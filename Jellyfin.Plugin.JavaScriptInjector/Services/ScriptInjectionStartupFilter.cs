using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavaScriptInjector.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavaScriptInjector.Services
{
    /// <summary>
    /// Injects the JavaScript Injector bootstrap block into jellyfin-web's index.html
    /// at request time. Jellyfin 12 can therefore use the plugin without File
    /// Transformation or writes to the web directory.
    /// </summary>
    public sealed class ScriptInjectionStartupFilter : IStartupFilter
    {
        private readonly ILogger<ScriptInjectionStartupFilter> _logger;
        private int _loggedOnce;

        public ScriptInjectionStartupFilter(ILogger<ScriptInjectionStartupFilter> logger)
        {
            _logger = logger;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(InvokeAsync);
                next(app);
            };
        }

        private async Task InvokeAsync(HttpContext context, Func<Task> nextMiddleware)
        {
            if (!HttpMethods.IsGet(context.Request.Method) || !IsIndexRequest(context.Request.Path.Value))
            {
                await nextMiddleware().ConfigureAwait(false);
                return;
            }

            // Request an uncompressed, complete document so the response can be
            // safely rewritten. Range responses are not useful for the app shell.
            context.Request.Headers.Remove("Accept-Encoding");
            context.Request.Headers.Remove("Range");
            context.Request.Headers.Remove("If-Range");

            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await nextMiddleware().ConfigureAwait(false);
            }
            catch
            {
                context.Response.Body = originalBody;
                throw;
            }

            context.Response.Body = originalBody;
            buffer.Seek(0, SeekOrigin.Begin);

            var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
                && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);

            if (!isHtml)
            {
                await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            string html;
            using (var reader = new StreamReader(buffer, Encoding.UTF8, true, 1024, leaveOpen: true))
            {
                html = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            try
            {
                var alreadyInjected = html.IndexOf("JavaScriptInjector/public.js", StringComparison.OrdinalIgnoreCase) >= 0;
                var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

                if (!alreadyInjected && bodyClose >= 0)
                {
                    var injectionBlock = JavascriptHelper.BuildInjectionBlock();
                    html = html.Substring(0, bodyClose) + injectionBlock + "\n" + html.Substring(bodyClose);

                    if (Interlocked.Exchange(ref _loggedOnce, 1) == 0)
                    {
                        _logger.LogInformation("Injected JavaScript Injector bootstrap via request-time middleware.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request-time JavaScript injection failed; serving the original web shell.");
            }

            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html;charset=utf-8";
            context.Response.ContentLength = bytes.Length;
            context.Response.Headers.Remove("ETag");
            context.Response.Headers.Remove("Last-Modified");
            context.Response.Headers.Remove("Accept-Ranges");
            await originalBody.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }

        private static bool IsIndexRequest(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web", StringComparison.OrdinalIgnoreCase);
        }
    }
}
