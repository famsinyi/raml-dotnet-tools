namespace Raml.Tools.XML
{
    public interface ICodeExtension
    {
        void Process(System.CodeDom.CodeNamespace code, System.Xml.Schema.XmlSchema schema);
    }
}