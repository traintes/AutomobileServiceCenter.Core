using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ASC.Web.Data;
using ASC.Web.Models;
using ASC.Web.Services;
using ASC.Web.Configuration;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace ASC.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see https://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets<Startup>();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Elcamino Azure Table Identity services.
            services.AddIdentity<ApplicationUser, IdentityRole>((options) =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddAzureTableStores<ApplicationDbContext>(new Func<IdentityConfiguration>(() =>
            {
                IdentityConfiguration idconfig = new IdentityConfiguration();
                idconfig.TablePrefix = Configuration
                    .GetSection("IdentityAzureTable:IdentityConfiguration:TablePrefix").Value;
                idconfig.StorageConnectionString = Configuration
                    .GetSection("IdentityAzureTable:IdentityConfiguration:StorageConnectionString").Value;
                idconfig.LocationMode = Configuration
                    .GetSection("IdentityAzureTable:IdentityConfiguration:LocationMode").Value;
                return idconfig;
            }))
            .AddDefaultTokenProviders()
            .CreateAzureTablesIfNotExists<ApplicationDbContext>();

            services.AddOptions();
            services.Configure<ApplicationSettings>(Configuration.GetSection("AppSettings"));

            services.AddDistributedMemoryCache();
            services.AddSession();

            services.AddMvc();

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
            services.AddSingleton<IIdentitySeed, IdentitySeed>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public async void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, IIdentitySeed storageSeed)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSession();
            app.UseStaticFiles();
            app.UseIdentity();

            app.UseGoogleAuthentication(new GoogleOptions()
            {
                ClientId = Configuration["Google:Identity:ClientId"],
                ClientSecret = Configuration["Google:Identity:ClientSecret"],
            });

            // Add external authentication middleware below. To configure them please see https://go.microsoft.com/fwlink/?LinkID=532715

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            await storageSeed.Seed(app.ApplicationServices.GetService<UserManager<ApplicationUser>>(),
                app.ApplicationServices.GetService<RoleManager<IdentityRole>>(),
                app.ApplicationServices.GetService<IOptions<ApplicationSettings>>());
        }
    }
}
