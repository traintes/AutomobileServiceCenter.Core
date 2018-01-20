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
using ASC.DataAccess.Interfaces;
using ASC.DataAccess;
using System.Reflection;
using ASC.Business.Interfaces;
using ASC.Business;
using AutoMapper;
using Newtonsoft.Json.Serialization;
using ASC.Web.Logger;
using ASC.Web.Filters;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

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
                options.Cookies.ApplicationCookie.AutomaticChallenge = false;
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

            //services.AddDistributedMemoryCache();
            services.AddDistributedRedisCache(options =>
            {
                options.Configuration = Configuration.GetSection("CacheSettings:CacheConnectionString").Value;
                options.InstanceName = Configuration.GetSection("CacheSettings:CacheInstance").Value;
            });

            services.AddSession();

            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.AddSignalR(options => options.Hubs.EnableDetailedErrors = true);

            services.AddMvc(o => { o.Filters.Add(typeof(CustomExceptionFilter)); })
                .AddJsonOptions(options
                    => options.SerializerSettings.ContractResolver = new DefaultContractResolver())
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix,
                    options => { options.ResourcesPath = "Resources"; })
                .AddDataAnnotationsLocalization(options => {
                    options.DataAnnotationLocalizerProvider =
                        (type, factory) => factory.Create(typeof(SharedResources));
                });

            services.Configure<RequestLocalizationOptions>(opts =>
            {
                List<CultureInfo> supportedCultures = new List<CultureInfo>
                {
                    new CultureInfo("en-US"),
                    new CultureInfo("es-MX")
                };

                opts.DefaultRequestCulture = new RequestCulture("en-US");
                // Formatting numbers, dates, etc.
                opts.SupportedCultures = supportedCultures;
                // UI strings that we have localized.
                opts.SupportedUICultures = supportedCultures;
            });

            services.AddAutoMapper();

            // Add support to embedded views from ASC.Utilities project.
            Assembly assembly = typeof(ASC.Utilities.Navigation.LeftNavigationViewComponent)
                .GetTypeInfo().Assembly;
            EmbeddedFileProvider embeddedFileProvider = new EmbeddedFileProvider(assembly, "ASC.Utilities");
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Add(embeddedFileProvider);
            });

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
            services.AddSingleton<IIdentitySeed, IdentitySeed>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IUnitOfWork>(p => 
                new UnitOfWork(Configuration.GetSection("ConnectionStrings:DefaultConnection").Value));
            services.AddScoped<IMasterDataOperations, MasterDataOperations>();
            services.AddSingleton<IMasterDataCacheOperations, MasterDataCacheOperations>();
            services.AddScoped<ILogDataOperations, LogDataOperations>();
            services.AddScoped<CustomExceptionFilter>();
            services.AddSingleton<INavigationCacheOperations, NavigationCacheOperations>();
            services.AddScoped<IServiceRequestOperations, ServiceRequestOperations>();
            services.AddScoped<IServiceRequestMessageOperations, ServiceRequestMessageOperations>();
            services.AddScoped<IOnlineUsersOperations, OnlineUsersOperations>();
            services.AddScoped<IPromotionOperations, PromotionOperations>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public async void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IIdentitySeed storageSeed,
            IMasterDataCacheOperations masterDataCacheOperations,
            ILogDataOperations logDataOperations,
            INavigationCacheOperations navigationCacheOperations,
            IUnitOfWork unitOfWork)
        {
            //loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            //loggerFactory.AddDebug();
            loggerFactory.AddConsole();
            // Configure Azure Logger to log all events except the ones that are generated by default
            // by ASP.NET Core.
            loggerFactory.AddAzureTableStorageLog(logDataOperations,
                (categoryName, logLevel)
                    => !categoryName.Contains("Microsoft") && logLevel >= LogLevel.Information);

            if (env.IsDevelopment())
            {
                //app.UseDeveloperExceptionPage();
                //app.UseDatabaseErrorPage();
                app.UseBrowserLink();
            }
            else
            {
                //app.UseExceptionHandler("/Home/Error");
            }

            IOptions<RequestLocalizationOptions> options =
                app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            app.UseRequestLocalization(options.Value);

            app.UseStatusCodePagesWithRedirects("/Home/Error/{0}");
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
                routes.MapRoute(name: "areaRoute",
                    template: "{area:exists}/{controller=Home}/{action=Index}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseWebSockets();
            app.UseSignalR();

            await storageSeed.Seed(app.ApplicationServices.GetService<UserManager<ApplicationUser>>(),
                app.ApplicationServices.GetService<RoleManager<IdentityRole>>(),
                app.ApplicationServices.GetService<IOptions<ApplicationSettings>>());

            IEnumerable<Type> models = Assembly.Load(new AssemblyName("ASC.Models")).GetTypes()
                .Where(type => type.Namespace == "ASC.Models.Models");
            foreach (Type model in models)
            {
                Object repositoryInstance = Activator.CreateInstance(typeof(Repository<>)
                    .MakeGenericType(model), unitOfWork);
                MethodInfo method = typeof(Repository<>).MakeGenericType(model)
                    .GetMethod("CreateTableAsync");
                method.Invoke(repositoryInstance, new object[0]);
            }

            await masterDataCacheOperations.CreateMasterDataCacheAsync();

            await navigationCacheOperations.CreateNavigationCacheAsync();
        }
    }
}
