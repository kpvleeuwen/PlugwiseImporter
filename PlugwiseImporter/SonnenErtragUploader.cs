using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Collections.Specialized;
using System.IO;

namespace PlugwiseImporter
{

    public class SonnenErtragUploader : IUploadMethod
    {
        private string _loginUri = @"http://www.solar-yield.eu/ajax/user/login";
        private string _insertUri = @"http://www.solar-yield.eu/plant/insertdatadaily";
        string _user;
        string _password;
        string _facilityId;

        public void Push(IEnumerable<YieldAggregate> applianceLog)
        {
            var credentials = GetCredentials();

            var logincookie = GetLoginSession(credentials);
            foreach (var monthlog in applianceLog.GroupBy(l => new { l.Date.Year, l.Date.Month }))
            {
                Console.WriteLine("Uploading {0}-{1}", monthlog.Key.Year, monthlog.Key.Month);
                UploadHistory(monthlog, logincookie);
            }
        }

        private void UploadHistory(IEnumerable<YieldAggregate> applianceLog, WebHeaderCollection logincookie)
        {
            var uri = new Uri(_insertUri);

            var values = new NameValueCollection();

            if (string.IsNullOrEmpty(_facilityId))
            {
                Console.WriteLine("No SonnenErtrag facilityId, not updating SonnenErtrag.");
            }

            Console.WriteLine("Uploading yield for FacilityId {0}", _facilityId);

            foreach (var log in applianceLog)
            {
                var dateformatted = log.Date.ToString("yyyy-MM-dd");
                values.Add(string.Format("yield[{0}]", dateformatted), log.Yield.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                values.Add(string.Format("is_auto_update[{0}]", dateformatted), "1");
            }

            values.Add("year", applianceLog.First().Date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add("save", "Save");
            values.Add("pb_id", _facilityId);
            values.Add("order", "asc");

            values.Add("month", applianceLog.First().Date.Month.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using (WebClient client = new WebClient())
            {
                client.Headers.Add(logincookie);
                var response = Encoding.ASCII.GetString(client.UploadValues(uri, values));
                Console.WriteLine("Success: {0}", response.Contains("Data saved!"));
                File.WriteAllText("response.html", response);
            }
        }

        private NetworkCredential GetCredentials()
        {
            // it is to be expected that not everybody likes to put their credentials in plain text on disk
            Utils.AskIfNullOrEmpty("Username:", ref _user);
            Utils.AskIfNullOrEmpty("Password:", ref _password);

            var credentials = new NetworkCredential(_user, _password);
            return credentials;
        }

        /// <summary>
        /// Logs in and returns the login cookie.
        /// Throws when login is not successful.
        /// </summary>
        /// <returns></returns>
        private WebHeaderCollection GetLoginSession(NetworkCredential credentials)
        {
            Console.WriteLine("Logging in as {0}", credentials.UserName);
            var uri = new Uri(_loginUri);
            NameValueCollection logindetails = new NameValueCollection
            {
                { "user", credentials.UserName},
                { "password", credentials.Password},
                { "submit", "Login" },
            };

            using (WebClient client = new WebClient())
            {
                client.Credentials = credentials;
                var response = Encoding.ASCII.GetString(client.UploadValues(uri, logindetails));
                Console.WriteLine("Login result: {0}", response);
                var result = new WebHeaderCollection();
                result.Add(HttpRequestHeader.Cookie, client.ResponseHeaders[HttpResponseHeader.SetCookie]);
                return result;
            }
        }


        public bool TryParse(string arg)
        {
            return Program.TryParse(arg, "seuser", ref _user, "SonnenErtrag user ID, will be asked when missing.")
                || Program.TryParse(arg, "sepass", ref _password, "SonnenErtrag password, will be asked when missing.")
                || Program.TryParse(arg, "sefacilityid", ref _facilityId, "SonnenErtrag FacilityID, when missing SonnenErtrag uploading is disabled.");
        }
    }
}
