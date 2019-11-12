using AMF.Tools.Core.ClientGenerator;

namespace AMF.Common
{
    public interface IT4Service
    {
        Result TransformText(string templatePath, ClientGeneratorModel model, string binPath, string ramlFile, string targetNamespace, 
            string testsNamespace = null);
        Result TransformText<T>(string templatePath, string paramName, T param, string binPath, string targetNamespace, string modelsNamespace,
            bool useAsyncMethods, bool includeHasModels = false, bool hasModels = true, bool includeApiVersionInRoutePrefix = false, string apiVersion = null, 
            string testsNamespace = null);
    }
}