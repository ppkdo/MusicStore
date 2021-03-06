using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authentication.Facebook;
using Microsoft.AspNet.Authentication.Twitter;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics;
using Microsoft.AspNet.Diagnostics.Entity;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Data.Entity;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using MusicStore.Models;

namespace MusicStore
{
    public class Startup
    {
        public Startup()
        {
            //Below code demonstrates usage of multiple configuration sources. For instance a setting say 'setting1' is found in both the registered sources, 
            //then the later source will win. By this way a Local config can be overridden by a different setting while deployed remotely.
            Configuration = new Configuration()
                        .AddJsonFile("config.json")
                        .AddEnvironmentVariables(); //All environment variables in the process's context flow in as configuration values.
        }

        public static IConfiguration Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            //Sql client not available on mono
            var useInMemoryStore = Type.GetType("Mono.Runtime") != null;

            // Add EF services to the services container
            if (useInMemoryStore)
            {
                services.AddEntityFramework()
                        .AddInMemoryStore()
                        .AddDbContext<MusicStoreContext>();
            }
            else
            {
                services.AddEntityFramework()
                        .AddSqlServer()
                        .AddDbContext<MusicStoreContext>(options =>
                            options.UseSqlServer(Configuration.Get("Data:DefaultConnection:ConnectionString")));
            }

            // Add Identity services to the services container
            services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<MusicStoreContext>()
                    .AddDefaultTokenProviders();

            services.ConfigureFacebookAuthentication(options =>
            {
                options.AppId = Configuration.Get("ExternalProviders:Facebook:AppId");
                options.AppSecret = Configuration.Get("ExternalProviders:Facebook:AppSecret");
                options.Scope.Add("email");
                options.Scope.Add("manage_pages");
                options.Notifications = new FacebookAuthenticationNotifications
                {
                    OnAuthenticated = context =>
                    {
                        var identity = context.Principal.Identities.First() ?? new ClaimsIdentity();
                        identity.AddClaim(new Claim("urn:tokens:facebook:access_token", context.AccessToken ?? ""));
                        identity.AddClaim(new Claim("urn:profile:facebook:name", context.Name ?? ""));
                        identity.AddClaim(new Claim("urn:profile:facebook:email", context.Email ?? ""));
                        return Task.FromResult(0);
                    }
                };
            });

            services.ConfigureGoogleAuthentication(options =>
            {
                options.ClientId = "977382855444.apps.googleusercontent.com";
                options.ClientSecret = "NafT482F70Vjj_9q1PU4B0pN";
            });

            services.ConfigureTwitterAuthentication(options =>
            {
                options.ConsumerKey = "9J3j3pSwgbWkgPFH7nAf0Spam";
                options.ConsumerSecret = "jUBYkQuBFyqp7G3CUB9SW3AfflFr9z3oQBiNvumYy87Al0W4h8";
                options.Notifications = new TwitterAuthenticationNotifications
                {
                    OnAuthenticated = context =>
                    {
                        var identity = context.Principal.Identities.First() ?? new ClaimsIdentity();
                        identity.AddClaim(new Claim("urn:tokens:twitter:accesstoken", context.AccessToken));
                        identity.AddClaim(new Claim("urn:tokens:twitter:accesstokensecret", context.AccessTokenSecret));
                        return Task.FromResult(0);
                    }
                };
                //options.Provier = new AuthenticationProvider()
                //{
                //    OnAuthenticated = async context =>
                //    {
                //        context.Identity.AddClaim(new Claim("urn:tokens:twitter:accesstoken", context.AccessToken));
                //        context.Identity.AddClaim(new Claim("urn:tokens:twitter:accesstokensecret", context.AccessTokenSecret));
                //    }
                //}
            });

            services.ConfigureMicrosoftAccountAuthentication(options =>
            {
                options.Caption = "MicrosoftAccount - Requires project changes";
                options.ClientId = "000000004012C08A";
                options.ClientSecret = "GaMQ2hCnqAC6EcDLnXsAeBVIJOLmeutL";
            });

            // Add MVC services to the services container
            services.AddMvc();

            //Add all SignalR related services to IoC.
            services.AddSignalR();

            //Add InMemoryCache
            services.AddSingleton<IMemoryCache, MemoryCache>();

            // Add session related services.
            services.AddCaching();
            services.AddSession();

            // Configure Auth
            services.Configure<AuthorizationOptions>(options =>
            {
                options.AddPolicy("ManageStore", new AuthorizationPolicyBuilder().RequireClaim("ManageStore", "Allowed").Build());
            });
        }

        //This method is invoked when ASPNET_ENV is 'Development' or is not defined
        //The allowed values are Development,Staging and Production
        public void ConfigureDevelopment(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            //Display custom error page in production when error occurs
            //During development use the ErrorPage middleware to display error information in the browser
            app.UseErrorPage(ErrorPageOptions.ShowAll);

            app.UseDatabaseErrorPage(DatabaseErrorPageOptions.ShowAll);

            // Add the runtime information page that can be used by developers
            // to see what packages are used by the application
            // default path is: /runtimeinfo
            app.UseRuntimeInfoPage();

            Configure(app);
        }

        //This method is invoked when ASPNET_ENV is 'Staging'
        //The allowed values are Development,Staging and Production
        public void ConfigureStaging(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            app.UseErrorHandler("/Home/Error");

            Configure(app);
        }

        //This method is invoked when ASPNET_ENV is 'Production'
        //The allowed values are Development,Staging and Production
        public void ConfigureProduction(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            app.UseErrorHandler("/Home/Error");

            Configure(app);
        }

        public void Configure(IApplicationBuilder app)
        {
            // Configure Session.
            app.UseSession();

            //Configure SignalR
            app.UseSignalR();

            // Add static files to the request pipeline
            app.UseStaticFiles();

            // Add cookie-based authentication to the request pipeline
            app.UseIdentity();

            app.UseFacebookAuthentication();

            app.UseGoogleAuthentication();

            app.UseTwitterAuthentication();

            // The MicrosoftAccount service has restrictions that prevent the use of http://localhost:5001/ for test applications.
            // As such, here is how to change this sample to uses http://ktesting.com:5001/ instead.

            // Edit the Project.json file and replace http://localhost:5001/ with http://ktesting.com:5001/.

            // From an admin command console first enter:
            // notepad C:\Windows\System32\drivers\etc\hosts
            // and add this to the file, save, and exit (and reboot?):
            // 127.0.0.1 ktesting.com

            // Then you can choose to run the app as admin (see below) or add the following ACL as admin:
            // netsh http add urlacl url=http://ktesting:5001/ user=[domain\user]

            // The sample app can then be run via:
            // dnx . web
            app.UseMicrosoftAccountAuthentication();

            // Add MVC to the request pipeline
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "areaRoute",
                    template: "{area:exists}/{controller}/{action}",
                    defaults: new { action = "Index" });

                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action}/{id?}",
                    defaults: new { controller = "Account", action = "ExternalLoginTest" });

                routes.MapRoute(
                    name: "api",
                    template: "{controller}/{id?}");
            });

            //Populates the MusicStore sample data
            SampleData.InitializeMusicStoreDatabaseAsync(app.ApplicationServices).Wait();
        }
    }
}