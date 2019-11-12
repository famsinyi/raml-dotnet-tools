﻿namespace AMF.Common
{
    public class RamlChooserActionParams
    {
        public RamlChooserActionParams(string ramlSource, string ramlFilePath, string ramlTitle, string templatesPath, string targetFileName, string targetNamespace,
            bool? doNotScaffold, bool? generateUnitTests = null, string testsProjectName = null, string testsNamespace = null)
        {
            RamlSource = ramlSource;
            RamlFilePath = ramlFilePath;
            RamlTitle = ramlTitle;
            TemplatesPath = templatesPath;
            TargetFileName = targetFileName;
            TargetNamespace = targetNamespace;
            DoNotScaffold = doNotScaffold;
            GenerateUnitTests = generateUnitTests;
            TestsNamespace = testsNamespace;
            TestsProjectName = testsProjectName;
        }

        public string RamlSource { get; private set; } // the original URL or file path of the RAML
        public string RamlFilePath { get; private set; } // file path of a temporary location of the RAML
        public string RamlTitle { get; private set; }
        public string TemplatesPath { get; private set; }
        public string TargetFileName { get; private set; }
        public string TargetNamespace { get; private set; }
        public bool? DoNotScaffold { get; private set; }
        public bool? GenerateUnitTests { get; set; }
        public string TestsNamespace { get; set; }
        public string TestsProjectName { get; set; }
        public bool UseAsyncMethods { get; set; }
        public string ClientRootClassName { get; set; }
        public bool IncludeApiVersionInRoutePrefix { get; set; }

        public string ModelsFolder { get; set; }

        public string BaseControllersFolder { get; set; }

        public string ImplementationControllersFolder { get; set; }
        public bool AddGeneratedSuffixToFiles { get; set; }
        public RamlInfo Data { get; set; }
        public string ModelsNamespace { get; internal set; }
        public string ControllersNamespace { get; internal set; }
    }
}