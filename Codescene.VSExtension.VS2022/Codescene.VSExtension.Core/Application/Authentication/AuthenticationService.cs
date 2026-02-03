using Codescene.VSExtension.Core.Interfaces.Authentication;
using Codescene.VSExtension.Core.Models;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net;

namespace Codescene.VSExtension.Core.Application.Authentication
{
    [Export(typeof(IAuthenticationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IPersistenceAuthDataProvider _persistenceDataProvider;

        [ImportingConstructor]
        public AuthenticationService(IPersistenceAuthDataProvider persistenceDataProvider)
        {
            _persistenceDataProvider = persistenceDataProvider;
        }

        const string NEXT = "/configuration/devtools-tokens/add/vscode"; // change later with visual studio next parameter

        private LoginResponse _loginResponse = null;

        public event AuthSignedInHandler OnSignedIn;
        public event AuthSignedOutHandler OnSignedOut;

        public LoginResponse GetData()
        {
            return _loginResponse;
        }

        public bool IsLoggedIn()
        {
            // First check in-memory object
            if (_loginResponse == null)
            {
                _loginResponse = _persistenceDataProvider.GetData();
            }

            return _loginResponse != null;
        }

        public bool Login(string serverUrl)
        {
            var authUrl = $"{serverUrl}?next={NEXT}";
            string redirectUri = "http://localhost:5000/callback/";


            // **** this line is temporary until we get implemted redirect on the Codescene side ************
            authUrl = "http://localhost:5000/callback/?token=0dbyR4WxyzwoIrb6eRVKRr5_PCSqrGPvr7ImdcUv_6Q.wun7jHqanjJ-wvAFYkU8Tim3cwGNZKgsMOa7taAvrRc&name=emirprljaca&user-id=66622";
            // ***************************************************************************************

            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var context = listener.GetContext();
            var request = context.Request;
            var response = GetResponse(request);

            listener.Stop();

            if (response != null)
            {
                _loginResponse = response;
                OnSignedIn?.Invoke(response);
                _persistenceDataProvider.Store(response);
                return true;
            }

            OnSignedOut?.Invoke();
            return false;
        }

        public void SignOut()
        {
            _loginResponse = null;
            _persistenceDataProvider.Clear();
            OnSignedOut?.Invoke();
        }

        private LoginResponse GetResponse(HttpListenerRequest request)
        {
            var token = request.QueryString["token"];
            var name = request.QueryString["name"];
            var userId = request.QueryString["user-id"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return new LoginResponse
            {
                Token = token,
                Name = name,
                UserId = userId
            };
        }
    }
}
