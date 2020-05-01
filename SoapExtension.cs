using System;
using System.IO;
using System.Web.Services.Protocols;

public class SoapLoggerExtension : SoapExtension
{
    public static string XmlRequest { get; private set; }
    public static string XmlResponse { get; private set; }

    private Stream oldStream;
    private Stream newStream;

    public override object GetInitializer(LogicalMethodInfo methodInfo, SoapExtensionAttribute attribute) { return null; }
    public override object GetInitializer(Type serviceType) { return null; }
    public override void Initialize(object initializer) { }

    public override Stream ChainStream(Stream stream)
    {
        oldStream = stream;
        newStream = new MemoryStream();
        return newStream;
    }

    public override void ProcessMessage(SoapMessage message)
    {
        switch (message.Stage)
        {
            case SoapMessageStage.AfterSerialize:
                newStream.Position = 0;
                XmlRequest = new StreamReader(newStream).ReadToEnd();
                newStream.Position = 0;
                newStream.CopyTo(oldStream);
                break;
            case SoapMessageStage.BeforeDeserialize:
                oldStream.CopyTo(newStream);
                newStream.Position = 0;
                XmlResponse = new StreamReader(newStream).ReadToEnd();
                newStream.Position = 0;
                break;
            default:
                break;
        }
    }
}

[AttributeUsage(System.AttributeTargets.Method)]
public class SoapLoggerExtensionAttribute : SoapExtensionAttribute
{
    public override int Priority { get; set; }
    public override System.Type ExtensionType { get { return typeof(SoapLoggerExtension); } }
}

