using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Services.Description;
using System.Xml.Serialization;
using System.Xml.Schema;

namespace DynamicWebServiceDriver
{
    class SchemaBuilder
    {
        public static Assembly BuildAssembly(Properties props, AssemblyName assemblyName, ref string nameSpace)
        {

            WebClient client = new WebClient();

            if (props.WSDLUri != null)
            {
                CredentialCache cc = new CredentialCache();
                cc.Add(new Uri(props.WSDLUri), props.AuthMode, props.GetCredentials());
                client.Credentials = cc;
            }

            ServiceDescription description;
            using (Stream stream = client.OpenRead(props.WSDLUri))
            {
                description = ServiceDescription.Read(stream);
            }

            ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
            //importer.ProtocolName = "Soap12"; 
            importer.AddServiceDescription(description, null, null);
            importer.Style = ServiceDescriptionImportStyle.Client;
            importer.CodeGenerationOptions = CodeGenerationOptions.GenerateProperties;

            CleanupSchema(props, client, description, importer);

            CodeNamespace cns = new CodeNamespace(nameSpace);
            CodeCompileUnit ccu = new CodeCompileUnit();
            ccu.Namespaces.Add(cns);

            ServiceDescriptionImportWarnings warning = importer.Import(cns, ccu);

            if (warning != ServiceDescriptionImportWarnings.NoCodeGenerated)
            {
                if (props.LogStreams)
                {
                    foreach (CodeMemberMethod cmm in ccu.Namespaces[0].Types.Cast<CodeTypeDeclaration>().SelectMany(t => t.Members.OfType<CodeMemberMethod>()
                                                                                                                          .Where(m => m.CustomAttributes.Cast<CodeAttributeDeclaration>()
                                                                                                                          .Any(c => c.Name.EndsWith("SoapDocumentMethodAttribute")))))
                    {
                        cmm.CustomAttributes.Add(new CodeAttributeDeclaration("SoapLoggerExtensionAttribute"));
                    }
                }

                if (props.FixNETBug)
                {
                    foreach (CodeTypeDeclaration cd in ccu.Namespaces[0].Types.Cast<CodeTypeDeclaration>()
                               .Where(c => c.CustomAttributes.Cast<CodeAttributeDeclaration>().All(cad => cad.AttributeType.BaseType != "System.Xml.Serialization.XmlTypeAttribute")
                                        && c.CustomAttributes.Cast<CodeAttributeDeclaration>().Any(cad => cad.AttributeType.BaseType != "System.SerializableAttribute")))
                    {
                        cd.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(System.Xml.Serialization.XmlTypeAttribute))));
                    }

                    foreach (CodeAttributeDeclaration cd in ccu.Namespaces[0].Types.Cast<CodeTypeDeclaration>()
                               .SelectMany(c => c.CustomAttributes.Cast<CodeAttributeDeclaration>())
                               .Where(cad => cad.AttributeType.BaseType == "System.Xml.Serialization.XmlTypeAttribute" && String.IsNullOrEmpty((string)cad.Arguments.Cast<CodeAttributeArgument>().Where(arg => arg.Name == "Namespace").Select(arg => ((CodePrimitiveExpression)arg.Value).Value).FirstOrDefault())
                                             && (cad.Arguments.Cast<CodeAttributeArgument>().All(arg => arg.Name != "IncludeInSchema")
                                                 || cad.Arguments.Cast<CodeAttributeArgument>().Single(arg => arg.Name == "IncludeInSchema").Value == new CodePrimitiveExpression(true))))
                    {
                        var a = cd.Arguments.Cast<CodeAttributeArgument>().Where(e => e.Name == "Namespace").FirstOrDefault();
                        if (a == null)
                        {
                            cd.Arguments.Add(new CodeAttributeArgument("Namespace", new CodePrimitiveExpression(description.TargetNamespace)));
                        }
                        else
                        {
                            a.Value = new CodePrimitiveExpression(description.TargetNamespace);
                        }
                    }
                }

                if (props.LogStreams)
                {
                    CodeMemberProperty cmmRawXmlResponse = new CodeMemberProperty();
                    cmmRawXmlResponse.Name = "RawXmlResponse";
                    cmmRawXmlResponse.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                    cmmRawXmlResponse.Type = new CodeTypeReference(typeof(string));
                    cmmRawXmlResponse.GetStatements.Add(new CodeMethodReturnStatement(new CodePropertyReferenceExpression(new CodeTypeReferenceExpression("SoapLoggerExtension"), "XmlResponse")));
                    ccu.Namespaces[0].Types[0].Members.Add(cmmRawXmlResponse);

                    CodeMemberProperty cmmRawXmlRequest = new CodeMemberProperty();
                    cmmRawXmlRequest.Name = "RawXmlRequest";
                    cmmRawXmlRequest.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                    cmmRawXmlRequest.Type = new CodeTypeReference(typeof(string));
                    cmmRawXmlRequest.GetStatements.Add(new CodeMethodReturnStatement(new CodePropertyReferenceExpression(new CodeTypeReferenceExpression("SoapLoggerExtension"), "XmlRequest")));
                    ccu.Namespaces[0].Types[0].Members.Add(cmmRawXmlRequest);
                }

                CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
                string[] assemblyRefs = new string[] { "System.dll", "System.Web.Services.dll", "System.Web.dll", "System.Xml.dll", "System.Data.dll", Assembly.GetExecutingAssembly().Location };
                CompilerParameters parms = new CompilerParameters(assemblyRefs);
                parms.GenerateInMemory = false;
                parms.CompilerOptions = "/optimize";
                parms.OutputAssembly = assemblyName.CodeBase;
                parms.IncludeDebugInformation = true;
                
                StringBuilder sb = new StringBuilder();
                provider.GenerateCodeFromCompileUnit(ccu, new StringWriter(sb), null);

                // Awfull hack to fix a weird bug with .Net proxy generation (???)
                sb = sb.Replace("[][]", "[]");
                
                CompilerResults cr = provider.CompileAssemblyFromSource(parms, sb.ToString());
                //cr = provider.CompileAssemblyFromDom(parms, ccu);

                if (props.KeepSource)
                {
                    //sb.ToString().Dump();
                }

                if (cr.Errors.Count > 0)
                {
                    throw new Exception("The assembly generation failed: " + String.Join("\r\n", cr.Errors.Cast<CompilerError>().Select(c => c.ErrorText).ToArray()));
                }
                else
                {
                    return cr.CompiledAssembly;
                }
            }

            return null;
        }

        private static void CleanupSchema(Properties props, WebClient client, ServiceDescription description, ServiceDescriptionImporter importer)
        {
            // Add any imported files
            Uri baseUri = new Uri(props.WSDLUri);

            XmlSchemaSet xss = new XmlSchemaSet();
            //xss.ValidationEventHandler += ((sender, args) => args.Message.Dump());

            List<string> done = new List<string>();
            foreach (XmlSchema sch in description.Types.Schemas.ToArray<XmlSchema>())
            {
                foreach (XmlSchemaExternal subSchema in sch.Includes.Cast<XmlSchemaExternal>().Where(s => !String.IsNullOrEmpty(s.SchemaLocation)))
                {
                    IncludeExtSchema(client, baseUri, xss, done, description.Namespaces, ((XmlSchemaImport)subSchema).Namespace, subSchema.SchemaLocation);
                }

                sch.Namespaces = description.Namespaces;
                sch.TargetNamespace = description.TargetNamespace;

                //TODO: essaie avec un filtrage des namespaces pour cibler une seule webméthode ?

                sch.Includes.Clear();
                xss.Add(sch);
            }

            //xss.Compile();

            importer.Schemas.Clear();
            foreach (XmlSchema xxx in xss.Schemas().Cast<XmlSchema>())
            {
                importer.Schemas.Add(xxx);
            }

            //	xss.GlobalTypes.Values.Count.Dump();
            //	importer.Schemas.SelectMany(x => x.SchemaTypes.Names.Cast<XmlQualifiedName>().Select(n => n.Namespace + "." + n.Name)).OrderBy(n => n).Dump();
        }

        private static void IncludeExtSchema(WebClient client, Uri baseUri, XmlSchemaSet xss, List<string> done, XmlSerializerNamespaces xmlNsp, string namespac, string location)
        {
            if (!done.Contains(location + namespac))
            {
                done.Add(location + namespac);

                Uri schUri = new Uri(baseUri, location);
                using (Stream str = client.OpenRead(schUri))
                {
                    XmlSchema subsch = XmlSchema.Read(str, null);

                    subsch.Namespaces = xmlNsp;
                    subsch.TargetNamespace = namespac;

                    //			if (namespac == null) 
                    //			{
                    //				subsch.AttributeFormDefault = XmlSchemaForm.Qualified;
                    //				subsch.ElementFormDefault = XmlSchemaForm.Qualified;
                    //			}

                    foreach (XmlSchemaExternal inc in subsch.Includes.Cast<XmlSchemaExternal>().Where(s => !String.IsNullOrEmpty(s.SchemaLocation)))
                    {
                        IncludeExtSchema(client, baseUri, xss, done, xmlNsp, null, inc.SchemaLocation);
                        IncludeExtSchema(client, baseUri, xss, done, xmlNsp, subsch.TargetNamespace, inc.SchemaLocation);
                    }

                    subsch.Includes.Clear();

                    xss.Add(subsch);
                }
            }
        }

    }
}
