using System.Net;
using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;
using System;

namespace DynamicWebServiceDriver
{
    /// <summary>
    /// Wrapper to expose typed properties over ConnectionInfo.DriverData.
    /// </summary>
    class Properties
    {
        readonly IConnectionInfo _cxInfo;
        readonly XElement _driverData;

        public Properties(IConnectionInfo cxInfo)
        {
            _cxInfo = cxInfo;
            _driverData = cxInfo.DriverData;
        }

        public bool Persist
        {
            get { return _cxInfo.Persist; }
            set { _cxInfo.Persist = value; }
        }

        public string Uri
        {
            get { return (string)_driverData.Element("Uri"); }
            set { _driverData.SetElementValue("Uri", value); }
        }

        public string WSDLUri
        {
            get { return (string)_driverData.Element("WSDLUri"); }
            set { _driverData.SetElementValue("WSDLUri", value); }
        }

        public bool AuthNone
        {
            get { return getBorDef(_driverData.Element("AuthNone")); }
            set { _driverData.SetElementValue("AuthNone", value); }
        }

        public bool AuthWindows
        {
            get { return getBorDef(_driverData.Element("AuthWindows")); }
            set { _driverData.SetElementValue("AuthWindows", value); }
        }

        public bool AuthSimple
        {
            get { return getBorDef(_driverData.Element("AuthSimple")); }
            set { _driverData.SetElementValue("AuthSimple", value); }
        }

        public string AuthMode
        {
            get { return (string)_driverData.Element("AuthMode") ?? "NTLM"; }
            set { _driverData.SetElementValue("AuthMode", value); }
        }

        public string Domain
        {
            get { return (string)_driverData.Element("Domain"); }
            set { _driverData.SetElementValue("Domain", value); }
        }

        public string UserName
        {
            get { return (string)_driverData.Element("UserName"); }
            set { _driverData.SetElementValue("UserName", value); }
        }

        public string Password
        {
            get { return _driverData.Element("Password") != null ? _cxInfo.Decrypt((string)_driverData.Element("Password")) : String.Empty; }
            set { _driverData.SetElementValue("Password", _cxInfo.Encrypt(value)); }
        }

        public bool ForceQualified
        {
            get { return getBorDef(_driverData.Element("ForceQualified")); }
            set { _driverData.SetElementValue("ForceQualified", value); }
        }

        public bool KeepSource
        {
            get { return getBorDef(_driverData.Element("KeepSource")); }
            set { _driverData.SetElementValue("KeepSource", value); }
        }

        public bool FixNETBug
        {
            get { return getBorDef(_driverData.Element("FixNETBug")); }
            set { _driverData.SetElementValue("FixNETBug", value); }
        }

        public bool LogStreams
        {
            get { return getBorDef(_driverData.Element("LogStreams")); }
            set { _driverData.SetElementValue("LogStreams", value); }
        }

        private bool getBorDef(XElement xe)
        {
            return xe == null ? false : (bool)xe;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public NetworkCredential GetCredentials()
        {
            if (AuthWindows)
            {
                return CredentialCache.DefaultNetworkCredentials;
            }
            else if (AuthSimple)
            {
                return new NetworkCredential(UserName, Password, Domain);
            }

            return null;
        }
    }

}
