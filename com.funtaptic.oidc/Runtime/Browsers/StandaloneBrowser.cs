using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Funtaptic.OIDC.Standalone
{
    namespace Funtaptic.OIDC.Auth
    {
        public class StandaloneBrowser : IBrowser
        {
            private HttpListener _httpListener;

            private Thread _listenerThread;

            private TaskCompletionSource<BrowserResult> _loginTaskCompletionSource;

            private OidcClient _client;

            public StandaloneBrowser()
            {
            }

            private void ListenForCallback()
            {
                while (_httpListener.IsListening)
                {
                    try
                    {
                        var context = _httpListener.GetContext();
                        var request = context.Request;
                        var response = context.Response;

                        WriteResponse(response, "You can now close this window and return to the game.");

                        var queryParams = HttpUtility.UrlDecode(request.Url.Query);
                        _loginTaskCompletionSource.SetResult(new BrowserResult()
                        {
                            ResultType = BrowserResultType.Success,
                            Response = queryParams
                        });
                        break;
                    }
                    catch (HttpListenerException e)
                    {
                        if (e.ErrorCode == 500) //listener closed
                            return;

                        throw;
                    }
                }
            }

            private static void WriteResponse(HttpListenerResponse response, string message)
            {
                var responseString =
                    $"<html><h1>{message}</h1></html>";

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }

            public async Task<BrowserResult> InvokeAsync(BrowserOptions options,
                CancellationToken cancellationToken = default)
            {
                _loginTaskCompletionSource = new TaskCompletionSource<BrowserResult>();

                var autoExpire = new CancellationTokenSource(options.Timeout);

                using var linkedSource =
                    CancellationTokenSource.CreateLinkedTokenSource(autoExpire.Token, cancellationToken);

                try
                {
                    var endUri = new Uri(options.EndUrl);
                    var toListen = endUri.GetLeftPart(UriPartial.Authority);

                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add(toListen + "/");
                    _httpListener.Start();

                    await using var canceled = cancellationToken.Register(() =>
                    {
                        _loginTaskCompletionSource.SetCanceled();
                        _httpListener.Stop();
                    });

                    await using var registration = autoExpire.Token.Register(() =>
                    {
                        _loginTaskCompletionSource.SetResult(new BrowserResult()
                        {
                            ResultType = BrowserResultType.Timeout,
                            Error = "Timed out"
                        });

                        _httpListener.Stop();
                    });

                    _listenerThread = new Thread(ListenForCallback);
                    _listenerThread.Start();
                    Application.OpenURL(options.StartUrl);
                    return await _loginTaskCompletionSource.Task;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    return new BrowserResult()
                    {
                        ResultType = BrowserResultType.UnknownError,
                        Error = e.Message
                    };
                }
                finally
                {
                    _httpListener.Stop();
                    _listenerThread.Join();
                }
            }
        }
    }
}