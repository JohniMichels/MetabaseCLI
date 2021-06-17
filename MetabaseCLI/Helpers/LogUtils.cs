
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public static class LogUtils
    {
        public static async Task<int> SafeHandleCommand(this ILogger logger, string commandName, Func<Task> command)
        {
            logger.LogCLICommandStarted(commandName);
            try
            {
                await command();
                logger.LogCLICommandFinishedSuccessfully(commandName);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogCLICommandFailure(commandName, ex);
                return 1;
            }
        }
        public static void LogCLICommandStarted(this ILogger logger, string commandName)
        {
            logger.LogTrace("Metabase CLI command {CLICommand} was called", commandName);
        }

        public static void LogCLICommandFailure(this ILogger logger, string commandName, Exception exception)
        {
            logger.LogCritical(exception, "Metabase CLI command {CLICommand} failed", commandName);
        }

        public static void LogCLICommandFinishedSuccessfully(this ILogger logger, string commandName)
        {
            logger.LogTrace("Metabase CLI command {CLICommand} finished successfully", commandName);
        }


        private static void LogWebRequestStart(
            this ILogger logger, string method, string server, 
            string url, object body, int hashId)
        {
            logger.LogTrace(
                "Sending {Method}:{RequestId} to {Server}/{Path} with '{@Content}'",
                method, hashId, server, url, body
            );
        }

        private static void LogWebRequestEnd(
            this ILogger logger, string method, string server, 
            string url, int hashId, long elapsedMilliseconds)
        {
            logger.LogTrace(
                "Request {Method}:{RequestId} to {Server}/{Path} returned after {RequestTime}ms",
                method, hashId, server, url,
                elapsedMilliseconds
            );
        }

        private static void LogWebRequestFailed(
            this ILogger logger, string method, string server, 
            string url, int hashId, Exception exception, long elapsedMilliseconds)
        {
            logger.LogError(
                exception,
                "Request {Method}:{RequestId} to {Server}/{Path} failed after {RequestTime}ms",
                method,
                hashId,
                server,
                url,
                elapsedMilliseconds
            );
        }

        public static async Task<HttpResponseMessage> LogWebRequest(
            this ILogger logger,
            string method,
            string server,
            string url,
            object body,
            Task<HttpResponseMessage> generator
        )
        {
            var hashId = Guid.NewGuid().GetHashCode();
            var stopWatch = new Stopwatch();
            var result = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            stopWatch.Start();
            try
            {
                logger.LogWebRequestStart(method, server, url, body, hashId);
                result = await generator;
                stopWatch.Stop();
                logger.LogWebRequestEnd(method, server, url, hashId, stopWatch.ElapsedMilliseconds);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                logger.LogWebRequestFailed(method, server, url, hashId, ex, stopWatch.ElapsedMilliseconds);
            }
            return result;
        }
    }
}