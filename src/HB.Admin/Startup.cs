using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using EasyCaching.Core;
using EasyCaching.InMemory;
using FluentValidation;
using FluentValidation.AspNetCore;
using HB.Admin.Configuration;
using HB.Admin.Models;
using HB.Admin.Services;
using HB.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HB.Admin
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddEasyCaching(option => option.UseInMemory("hb_memory_cache"));
            services.AddScoped<ICacheManagerService, MemoryCacheManagerService>();//��װһ�㣬�����õ���EasyCaching

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            HBConfiguration hbConfiguration = Configuration.GetSection("HBConfiguration").Get<HBConfiguration>();
            services.TryAddSingleton(hbConfiguration);

            services.AddHBDbContext(options => options.UseMySql(Configuration.GetConnectionString("MySqlConnection")));
            services.AddScoped<IHBDbContext, HBDbContext>();

            services.AddScoped<IMenuService, MenuService>();
            services.AddScoped<ISysAdminService, SysAdminService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IAuthenticationService, CookieAuthenticationService>();
            services.AddScoped<IWorkContextService, WorkContextService>();
            services.AddScoped<IPermissionService, PermissionService>();

            services.AddAuthentication(HBAuthenticationDefaults.AdminAuthenticationScheme)
                   .AddCookie(HBAuthenticationDefaults.AdminAuthenticationScheme, option =>
                   {
                       option.LoginPath = HBAuthenticationDefaults.LoginPath;
                       option.AccessDeniedPath = HBAuthenticationDefaults.AccessDeniedPath;
                       option.Cookie.Name = HBAuthenticationDefaults.AdminAuthenticationScheme;
                       
                   })
                   .AddCookie(HBAuthenticationDefaults.CustomerAuthenticationScheme, option =>
                   {
                       option.LoginPath = HBAuthenticationDefaults.SigninPath;
                   });

            services.AddAuthorization(options =>
                HBPermissionKeys.AllPermissions.ForEach(keys =>
                    options.AddPolicy(keys.Name, policy => policy.Requirements.Add(new AdminAuthorizationRequirement { Policy = keys.Name }))
            ));
            services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();

            var mvcBuilder = services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            mvcBuilder.AddFluentValidation(option =>
            {
                //https://docs.microsoft.com/zh-cn/aspnet/core/mvc/advanced/app-parts?view=aspnetcore-2.2
                var assemblies = mvcBuilder.PartManager.ApplicationParts
                 .OfType<AssemblyPart>()
                 .Where(part => part.Name.StartsWith("HB", StringComparison.InvariantCultureIgnoreCase))
                 .Select(part => part.Assembly);
                option.RegisterValidatorsFromAssemblies(assemblies);//�ӳ����м��ؼ̳�AbstractValidator<T> �� public�࣬���Ƿ� abstract��
                option.RunDefaultMvcValidationAfterFluentValidationExecutes = false;//ȥ��Ĭ����֤��ֻ����FluentValidation��֤
                option.ImplicitlyValidateChildProperties = true;//֧���������������֤      
            });
            ValidatorOptions.CascadeMode = CascadeMode.StopOnFirstFailure; //����ȫ�����ã���һ����֤ʧ������ֹͣ��֤

            services.AddAutoMapper(typeof(Startup));

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseStatusCodePagesWithRedirects("/page{0}.html"); //�ڸ�Ŀ¼�༭����״̬��Ĵ���ҳhttps://localhost:5001/page404.html
            }

            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                   name: "default",
                   template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
