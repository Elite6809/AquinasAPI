﻿using System;
using System.Net;
using System.Reflection;
using System.Xml.Linq;

namespace Aquinas.Api
{
    /// <summary>
    /// Represents a request to the MyAquinas API.
    /// </summary>
    public class ApiRequest
    {
        /// <summary>
        /// A web request to be sent to the server.
        /// </summary>
        private HttpWebRequest WebRequest;

        /// <summary>
        /// Create a new API request.
        /// </summary>
        /// <param name="authInfo">The authentication info to send to the server.</param>
        /// <param name="requestPath">The API request path. You can use the constants given in <see cref="ApiRequest"/>.</param>
        public ApiRequest(AuthenticationInfo authInfo, string requestPath)
        {
            if (!authInfo.Authenticated)
                throw new InvalidOperationException(Properties.Resources.ExceptionUnauthenticatedState);
            WebRequest = (HttpWebRequest)System.Net.WebRequest.Create(
                CreateApiUrl(authInfo, requestPath));
            WebRequest.Accept = "application/xml";

            PropertyInfo headerInfo = WebRequest.GetType().GetProperty("UserAgent");

            if (headerInfo != null)
            {
                headerInfo.SetValue(WebRequest, "MyAquinasMobileAPI", null);
            }
            else
            {
                WebRequest.Headers[HttpRequestHeader.UserAgent] = "MyAquinasMobileAPI";
            }
            
            WebRequest.Headers["AuthToken"] = authInfo.Token.ToString();
        }

        /// <summary>
        /// Begins an asynchronous API request.
        /// </summary>
        /// <param name="callback">The asynchronous callback to call upon completion of the request.</param>
        /// <returns>Returns the status of the asynchronous operation.</returns>
        public IAsyncResult BeginApiRequest(AsyncCallback callback)
        {
            return WebRequest.BeginGetResponse(callback, this);
        }

        /// <summary>
        /// Ends an asynchronous API request.
        /// </summary>
        /// <param name="result">The IAsyncResult object representing the status of the asynchronous operation.</param>
        /// <returns>An XDocument containing the result of the request.</returns>
        public XDocument EndApiRequest(IAsyncResult result)
        {
            ApiRequest apiRequest = (ApiRequest)result.AsyncState;
            HttpWebResponse response = (HttpWebResponse)apiRequest.WebRequest.EndGetResponse(result);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                XDocument document = XDocument.Load(response.GetResponseStream());
                return document;
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException(Properties.Resources.ExceptionBadLogin);
            }
            else
            {
                throw new Exception(
                    String.Format(Properties.Resources.ExceptionBadStatus,
                    response.StatusDescription));
            }
        }

        /// <summary>
        /// Creates a valid API URL.
        /// </summary>
        /// <param name="authInfo">Authentication info used to provide the admission number.</param>
        /// <param name="requestPath">The API request path. You can use the constants given in <see cref="ApiRequest"/>.</param>
        /// <returns></returns>
        private string CreateApiUrl(AuthenticationInfo authInfo, string requestPath)
        {
            return String.Format(Properties.Resources.ApiTemplateUrl,
                requestPath,
                authInfo.AdmissionNumber);
        }

        /// <summary>
        /// An API request to get basic student details.
        /// </summary>
        public const string GetStudentDetails = "Student/GetStudentDetails";

        /// <summary>
        /// An API request to get student timetable data.
        /// </summary>
        public const string GetTimetableData = "Student/gettimetable";
    }
}
