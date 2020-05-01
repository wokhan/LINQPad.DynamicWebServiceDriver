using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web.Services.Protocols;
using LINQPad.Extensibility.DataContext;

namespace DynamicWebServiceDriver
{
    public class Driver : DynamicDataContextDriver
    {
        public override string Name { get { return "Dynamic WebService Driver"; } }

        public override string Author { get { return "Khan"; } }

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            // The URI of the service best describes the connection:
            return new Properties(cxInfo).WSDLUri;
        }

        public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
        {
            // We need to pass the chosen URI into the DataServiceContext's constructor:
            return new ParameterDescriptor[] { };
        }

        public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
        {
            // We need to pass the chosen URI into the DataServiceContext's constructor:
            return new object[] { };
        }

        public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
        {
            // We need the following assembly for compilation and autocompletion:
            return new[] { "System.Web.Services.dll" };
        }

        public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
        {
            // Import the commonly used namespaces as a courtesy to the user:
            return new[] { "System.Web.Services.Protocols", "System.Web.Services.Description" };
        }

        public override bool AreRepositoriesEquivalent(IConnectionInfo r1, IConnectionInfo r2)
        {
            // Two repositories point to the same endpoint if their URIs are the same.
            return object.Equals(r1.DriverData.Element("WSDLUri"), r2.DriverData.Element("WSDLUri"))
                && object.Equals(r1.DriverData.Element("Uri"), r2.DriverData.Element("Uri"));
        }

        public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
        {
            var props = new Properties(cxInfo);
            SoapHttpClientProtocol dsContext = (SoapHttpClientProtocol)context;
            if (!String.IsNullOrEmpty(props.Uri))
            {
                dsContext.Url = props.Uri;
            }

            CredentialCache cc = new CredentialCache();
            cc.Add(new Uri(dsContext.Url), props.AuthMode, props.GetCredentials());
            dsContext.Credentials = cc;
          
        }

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
        {
            // Populate the default URI with a demo value:
            if (isNewConnection) new Properties(cxInfo).WSDLUri = "";

            bool? result = new ConnectionDialog(cxInfo).ShowDialog();
            return result == true;
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
        {
            var asm = SchemaBuilder.BuildAssembly(new Properties(cxInfo), assemblyToBuild, ref nameSpace);

            return GetSchema(asm, out typeName);
        }

        static List<ExplorerItem> GetSchema(Assembly asm, out string typeName)
        {
            Type t = asm.GetTypes()[0];
            typeName = t.Name;

            return new List<ExplorerItem> {
                new ExplorerItem("Methods", ExplorerItemKind.Category, ExplorerIcon.Box) {
                    Children = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .Where(m => !m.IsSpecialName)
                                .OrderBy(m => m.Name)
                                .Select(m => new ExplorerItem(m.Name, ExplorerItemKind.QueryableObject, ExplorerIcon.TableFunction) {
                                                              Children = m.GetParameters().Select(p => new ExplorerItem(String.Format("{0} ({1})", p.Name, p.ParameterType.Name), ExplorerItemKind.Parameter, ExplorerIcon.Parameter))
                                                                                          .ToList()
                                })
                                .ToList()
                },
                new ExplorerItem("Types", ExplorerItemKind.Category, ExplorerIcon.Box) {
                    Children = asm.GetTypes()
                                .OrderBy(st => st.Name)
                                .Select(st => new ExplorerItem(st.Name, ExplorerItemKind.Property, ExplorerIcon.Parameter) {
                                    Children = st.GetProperties().Select(p => new ExplorerItem(String.Format("{0} ({1})", p.Name, p.PropertyType.Name), ExplorerItemKind.Parameter, ExplorerIcon.Parameter))
                                                                 .ToList()
                                })
                                .ToList()
                }};
        }

    }
}
