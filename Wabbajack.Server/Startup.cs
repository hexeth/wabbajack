﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;

namespace Wabbajack.Server
{
    public class TestStartup : Startup
    {
        public TestStartup(IConfiguration configuration) : base(configuration)
        {
        }
    }
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
            Consts.UseNetworkWorkaroundMode = true;
            Helpers.Init();
            /*services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "Wabbajack Build API", Version = "v1"});
            });*/
            
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                    options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                })
                .AddApiKeySupport(options => {});
            
            services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = int.MaxValue;
                x.MultipartBodyLengthLimit = int.MaxValue;
            });

            services.AddSingleton<AppSettings>();
            services.AddSingleton<QuickSync>();
            services.AddSingleton<SqlService>();
            services.AddSingleton<GlobalInformation>();
            services.AddSingleton<NexusPoll>();
            services.AddSingleton<ArchiveMaintainer>();
            services.AddSingleton<ModListDownloader>();
            services.AddSingleton<NonNexusDownloadValidator>();
            services.AddSingleton<ListValidator>();
            services.AddSingleton<ArchiveDownloader>();
            services.AddSingleton<DiscordWebHook>();
            services.AddSingleton<NexusKeyMaintainance>();
            services.AddSingleton<PatchBuilder>();
            services.AddSingleton<CDNMirrorList>();
            services.AddSingleton<NexusPermissionsUpdater>();
            services.AddSingleton<MirrorUploader>();
            services.AddSingleton<MirrorQueueService>();
            services.AddSingleton<Watchdog>();
            services.AddSingleton<DiscordFrontend>();
            services.AddSingleton<AuthoredFilesCleanup>();
            services.AddSingleton<MetricsKeyCache>();
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = new[] {"application/json"};
            });
            
            services.AddMvc();
            services.AddControllers()
                .AddNewtonsoftJson(o =>
            {
                
                o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });
            
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (!(this is TestStartup)) 
                app.UseHttpsRedirection();
            
            app.UseDeveloperExceptionPage();
            
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".rar"] = "application/x-rar-compressed";
            provider.Mappings[".7z"] = "application/x-7z-compressed";
            provider.Mappings[".zip"] = "application/zip";
            provider.Mappings[".wabbajack"] = "application/zip";
            app.UseStaticFiles();

            /*
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wabbajack Build API");
                c.RoutePrefix = string.Empty;
            }); */
            
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseNexusPoll();
            //app.UseArchiveMaintainer();
            //app.UseModListDownloader();
            app.UseResponseCompression();
            
            //app.UseService<NonNexusDownloadValidator>();
            //app.UseService<ListValidator>();
            //app.UseService<ArchiveDownloader>();
            app.UseService<DiscordWebHook>();
            //app.UseService<NexusKeyMaintainance>();
            //app.UseService<PatchBuilder>();
            //app.UseService<CDNMirrorList>();
            //app.UseService<NexusPermissionsUpdater>();
            //app.UseService<MirrorUploader>();
            //app.UseService<MirrorQueueService>();
            app.UseService<Watchdog>();
            app.UseService<DiscordFrontend>();
            // Don't enable Author Files Cleanup
            //app.UseService<AuthoredFilesCleanup>();
            app.UseService<MetricsKeyCache>();

            app.Use(next =>
            {
                return async context =>
                {
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    context.Response.OnStarting(() =>
                    {
                        stopWatch.Stop();
                        var headers = context.Response.Headers;
                        headers.Add("Access-Control-Allow-Origin", "*");
                        headers.Add("Access-Control-Allow-Methods", "POST, GET");
                        headers.Add("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
                        headers.Add("X-ResponseTime-Ms", stopWatch.ElapsedMilliseconds.ToString());
                        if (!headers.ContainsKey("Cache-Control")) 
                            headers.Add("Cache-Control", "no-cache");
                        return Task.CompletedTask;
                    });
                    await next(context);
                };
            });
            
            app.UseFileServer(new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "public")),
                StaticFileOptions = {ServeUnknownFileTypes = true},
                
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
