using System.Net;
using Microsoft.AspNetCore.Http;

namespace Arashi
{
    public class RealIP
    {
        public static IPAddress Get(HttpContext context)
        {
            try
            {
                var request = context.Request;
                if (request.Headers.ContainsKey("Fastly-Client-IP"))
                {
                    var addr = IPEndPoint.Parse(request.Headers["Fastly-Client-IP"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                    return addr;
                }

                if (request.Headers.ContainsKey("X-Vercel-Forwarded-For"))
                {
                    var addr = IPEndPoint.Parse(request.Headers["X-Vercel-Forwarded-For"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                    return addr;
                }

                if (request.Headers.ContainsKey("CF-Connecting-IP"))
                {
                    var addr = IPEndPoint.Parse(request.Headers["CF-Connecting-IP"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                    return addr;
                }

                if (request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    var addr = IPEndPoint.Parse(request.Headers["X-Forwarded-For"].ToString().Split(',')
                        .FirstOrDefault().Trim()).Address;
                    return addr;
                }

                if (request.Headers.ContainsKey("X-Real-IP"))
                {
                    var addr = IPEndPoint
                        .Parse(request.Headers["X-Real-IP"].ToString().Split(',').FirstOrDefault().Trim())
                        .Address;
                    return addr;
                }

                return context.Connection.RemoteIpAddress;
            }
            catch (Exception)
            {
                try
                {
                    return context.Connection.RemoteIpAddress;
                }
                catch (Exception)
                {
                    return IPAddress.Any;
                }
            }
        }
    }
}
