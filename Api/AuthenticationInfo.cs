﻿using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Aquinas.Api
{
    /// <summary>
    /// Represents authentication info used to connect to the server.
    /// </summary>
    public class AuthenticationInfo
    {
        /// <summary>
        /// The long-form admission number.
        /// </summary>
        public string AdmissionNumber
        {
            get;
            private set;
        }

        /// <summary>
        /// The user's password.
        /// </summary>
        private string Password
        {
            get; set;
        }

        /// <summary>
        /// The token GUID used to validate requests.
        /// </summary>
        public Guid Token
        {
            get
            {
                if (Authenticated)
                    return _Token;
                else
                    throw new InvalidOperationException(Properties.Resources.ExceptionNotYetAuthenticated);
            }
            private set
            {
                _Token = value;
            }
        }

        /// <summary>
        /// Gets whether this AuthenticationInfo has been authenticated by the MyAquinas server.
        /// </summary>
        public bool Authenticated
        {
            get;
            private set;
        }

        private Guid _Token;
        private HttpWebRequest Request;

        /// <summary>
        /// Create a new AuthenticationInfo object.
        /// </summary>
        /// <param name="admissionNumber">The long-form admission number.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="token">The token GUID used to validate requests.</param>
        public AuthenticationInfo(string admissionNumber, string password, Guid token)
            : this(admissionNumber, password)
        {
            Token = token;
            Authenticated = true;
        }

        /// <summary>
        /// Create a new AuthenticationInfo object.
        /// </summary>
        /// <param name="admissionNumber">The long-form admission number.</param>
        /// <param name="password">The user's password.</param>
        public AuthenticationInfo(string admissionNumber, string password)
        {
            AdmissionNumber = admissionNumber;
            Password = password;
            Authenticated = false;
        }

        /// <summary>
        /// Builds the request body in XML form.
        /// </summary>
        /// <returns>The request body to send to the server.</returns>
        private XDocument BuildRequestBody()
        {
            XDocument document = new XDocument();
            XElement rootElement = new XElement("AuthDetails",
                new XElement("AdmissionNo", AdmissionNumber),
                new XElement("Password", Password));
            document.Add(rootElement);
            return document;
        }

        /// <summary>
        /// Begins an asynchronous API authentication request.
        /// </summary>
        /// <param name="callback">The asynchronous callback for the web request</param>
        /// <returns>An XDocument containing the result of the request.</returns>
        public IAsyncResult BeginAuthenticate(AsyncCallback callback)
        {
            Request = (HttpWebRequest)System.Net.WebRequest.Create(
                Properties.Resources.AuthenticationUrl);
            Request.Method = "POST";
            Request.ContentType = "application/xml";
            PropertyInfo headerInfo = Request.GetType().GetProperty("UserAgent");

            if (headerInfo != null)
            {
                headerInfo.SetValue(Request, "MyAquinasMobileAPI", null);
            }
            else
            {
                Request.Headers[HttpRequestHeader.UserAgent] = "MyAquinasMobileAPI";
            }

            Request.Accept = "application/xml"; //Had to add this to stop the server returning JSON
            AuthenticationState state = new AuthenticationState(callback, Request);
            return Request.BeginGetRequestStream(AuthenticateWriteAndSend, state);
        }

        /// <summary>
        /// Writes the authentication data to the stream and sends it asynchronously.
        /// </summary>
        /// <param name="result">The IAsyncResult object representing the status of the asynchronous operation.</param>
        private void AuthenticateWriteAndSend(IAsyncResult result)
        {
            AuthenticationState state = ((AuthenticationState)result.AsyncState);
            HttpWebRequest request = state.Request;
            using (Stream requestStream = request.EndGetRequestStream(result))
            {
                XDocument requestBody = BuildRequestBody();
                requestBody.Save(requestStream);
            }
            request.BeginGetResponse(state.Callback, this);
        }

        /// <summary>
        /// Ends an asynchronous API request and sets this object's value to the result of the operation.
        /// </summary>
        /// <param name="result">The IAsyncResult object representing the status of the asynchronous operation.</param>
        /// <returns>Returns this object.</returns>
        public AuthenticationInfo EndAuthenticate(IAsyncResult result)
        {
            try
            {
                HttpWebRequest request = (result.AsyncState as AuthenticationInfo).Request;
                HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(result); // this is what was throwing the exception about the 405 Method Not Allowed

                XDocument document = XDocument.Load(response.GetResponseStream());
                XElement rootElement = document.Root;

                XElement tokenElement = rootElement.Element(XName.Get("Token", Properties.Resources.XmlNamespace));
                if (tokenElement != null)
                {
                    Token = new Guid(tokenElement.Value);
                    Authenticated = true;
                }
                else
                {
                    throw new NullReferenceException(Properties.Resources.ExceptionTokenNotReceived);
                }
                return this;
            }
            catch (WebException e)
            {
                if ((int)((HttpWebResponse)e.Response).StatusCode == 500)
                {
                    throw new ApiException(e.Message, ApiExceptionDetails.BadLogin, e);
                }
                else
                {
                    throw new ApiException(e.Message, ApiExceptionDetails.BadHttpStatus, e);
                }
            }
        }
    }
}
