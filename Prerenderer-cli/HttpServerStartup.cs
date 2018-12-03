using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace com.inspirationlabs.prerenderer
{
    public class HttpServerStartup
    {
        public static string basePath = "";
        public void Configure(IApplicationBuilder app)
        {

            app.Use(async (ctx, next) =>
            {
                if(basePath.Length > 0)
                {
                    if(ctx.Request.Path.Value.StartsWith(basePath))
                    {
                        string repl = ctx.Request.Path.Value.Remove(0, basePath.Length);
                        if(!repl.StartsWith("/")) {
                            repl = "/" + repl;
                        }
                        //ctx.Request.Path = new PathString(ctx.Request.Path.Value.Replace(basePath, ""));
                        ctx.Request.Path = repl;
                    }
                }

                await next();

                if (ctx.Response.StatusCode == 404 && !ctx.Response.HasStarted)
                {
                    // Re - execute the request so the user gets the error page
                    string originalPath = ctx.Request.Path.Value;
                    ctx.Items["originalPath"] = originalPath;
                    ctx.Request.Path = "/index.html";
                    await next();
                }
            });

            app.UseFileServer();

        }
    }
}