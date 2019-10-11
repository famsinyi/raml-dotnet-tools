﻿using Raml.Parser.Expressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Description;
using RAML.Api.Core;

namespace RAML.WebApiExplorer
{
	public abstract class ApiExplorerService
	{
		private readonly IApiExplorer apiExplorer;
		private readonly string baseUri;
		private SecurityScheme securityScheme;
		private string securityType;

		public IEnumerable<string> SecuredBy { get; set; }

		public IEnumerable<IDictionary<string, SecurityScheme>> SecuritySchemes { get; set; }

		public IEnumerable<Protocol> Protocols { get; set; }

        public string DefaultMediaType { get; set; }

        protected readonly RamlTypesOrderedDictionary RamlTypes = new RamlTypesOrderedDictionary();
        protected readonly IDictionary<string, string> Schemas = new Dictionary<string, string>();
	    protected readonly ICollection<Type> Types = new Collection<Type>();

	    public ApiExplorerService(IApiExplorer apiExplorer, string baseUri = null)
		{
			this.apiExplorer = apiExplorer;
			this.baseUri = baseUri;
		}

		public RamlDocument GetRaml(RamlVersion ramlVersion = RamlVersion.Version1, string title = null)
		{
			if (string.IsNullOrWhiteSpace(title))
				title = "Api";

			var raml = new RamlDocument {Title = title, BaseUri = baseUri, RamlVersion = ramlVersion };
			var resourcesDic = new Dictionary<string, Resource>();
			var parameterDescriptionsDic = new Dictionary<string, Collection<ApiParameterDescription>>();
			foreach (var api in apiExplorer.ApiDescriptions)
			{
				var relativeUri = !api.Route.RouteTemplate.StartsWith("/") ? "/" + api.Route.RouteTemplate : api.Route.RouteTemplate;
				if (relativeUri.Contains("{controller}"))
				{
					relativeUri = relativeUri.Replace("{controller}", api.ActionDescriptor.ControllerDescriptor.ControllerName);

					if (relativeUri.EndsWith("/"))
						relativeUri = relativeUri.Substring(0, relativeUri.Length - 1);
				}

				foreach (var apiParam in GetParametersFromUrl(relativeUri))
				{
					relativeUri = RemoveNonExistingParametersFromRoute(relativeUri, api, apiParam.Key);
				}

				Resource resource;
				if (!resourcesDic.ContainsKey(relativeUri))
				{
					resource = new Resource
					               {
						               Methods = GetMethods(api, new Collection<string>()),
									   RelativeUri = relativeUri,
					               };
					parameterDescriptionsDic.Add(relativeUri, api.ParameterDescriptions);
					resourcesDic.Add(relativeUri, resource);
				}
				else
				{
					resource = resourcesDic[relativeUri];
					foreach (var apiParameterDescription in api.ParameterDescriptions)
					{
						parameterDescriptionsDic[relativeUri].Add(apiParameterDescription);
					}
					AddMethods(resource, api, resource.Methods.Select(m => m.Verb).ToList());
				}

				if(SetResourceProperties != null)
					SetResourceProperties(resource, api);

				if(SetResourcePropertiesByAction != null)
					SetResourcePropertiesByAction(resource, api.ActionDescriptor);

				if(SetResourcePropertiesByController != null)
					SetResourcePropertiesByController(resource, api.ActionDescriptor.ControllerDescriptor);
			}

		    raml.Schemas = new List<IDictionary<string, string>> { Schemas };

		    raml.Types = RamlTypes;

			OrganizeResourcesHierarchically(raml, resourcesDic);

			SetUriParameters(raml.Resources, parameterDescriptionsDic, string.Empty);

			if(SetRamlProperties != null)
				SetRamlProperties(raml);

			if (SecuritySchemes != null)
				raml.SecuritySchemes = SecuritySchemes;
			
			if (!string.IsNullOrWhiteSpace(securityType) && securityScheme != null)
				SetSecurityScheme(raml);

			if (SecuredBy != null)
				raml.SecuredBy = SecuredBy;

			if(Protocols != null)
				raml.Protocols = Protocols;

			return raml;
		}

		public void SetSecurityScheme(string type, SecurityScheme scheme)
		{
			securityScheme = scheme;
			securityType = type;
		}

		public void UseOAuth2(string authorizationUri, string accessTokenUri, IEnumerable<string> authorizationGrants, IEnumerable<string> scopes, SecuritySchemeDescriptor securitySchemeDescriptor)
		{
			securityType = "oauth_2_0";
			var securitySettings = new SecuritySettings
			                       {
				                       AuthorizationUri = authorizationUri,
				                       AccessTokenUri = accessTokenUri,
				                       AuthorizationGrants = authorizationGrants,
				                       Scopes = scopes
			                       };
			securityScheme = new SecurityScheme
			                 {
				                 DescribedBy = securitySchemeDescriptor,
				                 Settings = securitySettings,
				                 Type = new Dictionary<string, IDictionary<string, string>> {{"OAuth 2.0", null}}
			                 };
		}

		public void UseOAuth1(string authorizationUri, string requestTokenUri, string tokenCredentialsUri, SecuritySchemeDescriptor securitySchemeDescriptor)
		{
			securityType = "oauth_1_0";
			var securitySettings = new SecuritySettings
			                       {
				                       AuthorizationUri = authorizationUri,
				                       RequestTokenUri = requestTokenUri,
				                       TokenCredentialsUri = tokenCredentialsUri
			                       };
			securityScheme = new SecurityScheme
			                 {
				                 DescribedBy = securitySchemeDescriptor,
				                 Settings = securitySettings,
				                 Type = new Dictionary<string, IDictionary<string, string>> {{"OAuth 1.0", null}}
			                 };
		}

		public Action<RamlDocument> SetRamlProperties { get; set; }

		public Action<Resource, ApiDescription> SetResourceProperties  { get; set; }

		public Action<Resource, HttpControllerDescriptor> SetResourcePropertiesByController { get; set; }

		public Action<Resource, HttpActionDescriptor> SetResourcePropertiesByAction { get; set; }

        public Action<ApiDescription, Method> SetMethodProperties { get; set; }

		private void SetSecurityScheme(RamlDocument raml)
		{
			var securitySchemes = new List<IDictionary<string, SecurityScheme>>();

			if (raml.SecuritySchemes != null && raml.SecuritySchemes.Any())
				securitySchemes = raml.SecuritySchemes.ToList();

			var schemes = new Dictionary<string, SecurityScheme> { { securityType, securityScheme } };
			securitySchemes.Add(schemes);

			raml.SecuritySchemes = securitySchemes;
		}

		private static string RemoveNonExistingParametersFromRoute(string relativeUri, ApiDescription api, string parameterName)
		{
			// if the parameter in the route is not a parameter in the method then it does not use it so I remove it from the route
			var parameterString = "{" + parameterName.ToLowerInvariant() + "}";
			if (relativeUri.Length > parameterString.Length + 1
			    && api.ParameterDescriptions.All(p => p.Name.ToLowerInvariant() != parameterName.ToLowerInvariant()))
			{
				relativeUri = relativeUri.Replace(parameterString, string.Empty);
			}

			return relativeUri;
		}

		private void SetUriParameters(IEnumerable<Resource> resources, Dictionary<string, Collection<ApiParameterDescription>> parameterDescriptionsDic, string parentUrl)
		{
			if(resources == null)
				return;

			foreach (var resource in resources)
			{
				var fullUrl = parentUrl + resource.RelativeUri;
				resource.UriParameters = GetUriParameters(resource.RelativeUri, parameterDescriptionsDic[fullUrl]);
				SetUriParameters(resource.Resources, parameterDescriptionsDic, fullUrl);
			}
		}

		private void OrganizeResourcesHierarchically(RamlDocument raml, Dictionary<string, Resource> resourcesDic)
		{
			foreach (var kv in resourcesDic)
			{
				var matchingResources = resourcesDic.Where(r => r.Key != kv.Key && kv.Key.StartsWith(r.Key + "/"));
				if (matchingResources.Any())
				{
					var parent = matchingResources.OrderByDescending(r => r.Key.Length).First();
					kv.Value.RelativeUri = kv.Value.RelativeUri.Substring(parent.Key.Length); // remove parent route from relative uri
					parent.Value.Resources.Add(kv.Value);
				}
				else
				{
					raml.Resources.Add(kv.Value);
				}
			}
		}

		private void AddMethods(Resource resource, ApiDescription api, ICollection<string> verbs)
		{
			var methods = resource.Methods.ToList();
			var newMethods = GetMethods(api, verbs);
			methods.AddRange(newMethods);
			resource.Methods = methods;
		}

		private string GetDescription(ApiDescription api)
		{
			var description = string.Empty;
			
			if (!string.IsNullOrWhiteSpace(api.Documentation))
				description += api.Documentation;

			description += " (" + api.ActionDescriptor.ControllerDescriptor.ControllerName + "." + api.ActionDescriptor.ActionName + ")";

			return description;
		}

		private IEnumerable<Method> GetMethods(ApiDescription api, ICollection<string> verbs)
		{
			var methods = new Collection<Method>();
			foreach (var httpMethod in api.ActionDescriptor.SupportedHttpMethods)
			{
				var verb = httpMethod.Method.ToLowerInvariant();
				if (verbs.Contains(verb)) 
					continue;

				var method = new Method
				             {
					             Description = GetDescription(api),
					             Verb = verb,
					             QueryParameters = GetQueryParameters(api.ParameterDescriptions), // GetQueryParameters(api.RelativePath, api.ParameterDescriptions),
					             Body = GetRequestMimeTypes(api),
					             Responses = GetResponses(api.ResponseDescription, api),
				             };
				methods.Add(method);
                verbs.Add(verb);

                if (SetMethodProperties != null)
                    SetMethodProperties(api, method);

			}
			return methods;
		}

		private IEnumerable<Response> GetResponses(ResponseDescription responseDescription, ApiDescription api)
		{
            var responses = new List<Response>();

			if (responseDescription.ResponseType == null && responseDescription.DeclaredType == null)
				return responses;

			var responseType = responseDescription.ResponseType ?? responseDescription.DeclaredType;

            var attributes = api.ActionDescriptor.GetCustomAttributes<Attribute>();
            responses = HandleResponseTypeStatusAttributes(attributes);

            if(responseType == typeof(IHttpActionResult))
                return responses;

            responses.Add(HandleResponseTypeAttributes(responseType));

			return responses;
		}

	    private Response HandleResponseTypeAttributes(Type responseType)
	    {
            var type = AddType(responseType);

            return new Response
            {
                Body = CreateJsonMimeType(type),
                Code = "200"
            };
	    }

	    private List<Response> HandleResponseTypeStatusAttributes(IEnumerable<Attribute> attributes)
	    {
            var responses = new Dictionary<string,Response>();
            foreach (var attribute in attributes.Where(a => a is ResponseTypeStatusAttribute))
            {
                var response = GetResponse(attribute);
                if(!responses.ContainsKey(response.Code))
                    responses.Add(response.Code, response);
            }
	        return responses.Values.ToList();
	    }

	    private Response GetResponse(Attribute attribute)
	    {
            var status = ((ResponseTypeStatusAttribute)attribute).StatusCode;
            var type = ((ResponseTypeStatusAttribute)attribute).ResponseType;
            var typeName = AddType(type);
            return new Response
            {
                Code = ((int)status).ToString(CultureInfo.InvariantCulture),
                Body = CreateJsonMimeType(typeName)
            };
	    }

        protected Dictionary<string, MimeType> CreateJsonMimeType(string type)
        {
            var mimeType = CreateMimeType(type);
            return CreateMimeTypes(mimeType);
        }


	    protected Dictionary<string, MimeType> CreateMimeTypes(MimeType mimeType)
	    {
	        var mimeTypes = new Dictionary<string, MimeType>
	        {
	            {
	                "application/json",
	                mimeType
	            }
	        };
	        return mimeTypes;
	    }


	    protected abstract string AddType(Type type);

	    protected string GetUniqueSchemaName(string schemaName)
	    {
	        for (var i = 0; i < 1000; i++)
	        {
	            schemaName += i;
	            if (!Schemas.ContainsKey(schemaName))
	                return schemaName;
	        }
            throw new InvalidOperationException("Could not find a unique name. You have more than 1000 types with the same class name");
	    }

	    private Dictionary<string, MimeType> GetRequestMimeTypes(ApiDescription api)
		{
			var mediaTypes = api.SupportedRequestBodyFormatters.SelectMany(f => f.SupportedMediaTypes).ToArray();
			var mimeTypes = new Dictionary<string, MimeType>();

			var apiParam = api.ParameterDescriptions.FirstOrDefault(p => p.Source == ApiParameterSource.FromBody);
			MimeType mimeType = null;

			if (apiParam != null)
			{
				var type = apiParam.ParameterDescriptor.ParameterType;

                var typeName = AddType(type);

				mimeType = CreateMimeType(typeName);
			}

			if(mimeType != null && !mediaTypes.Any())
				mimeTypes.Add("application/json", mimeType);

			foreach (var mediaType in mediaTypes)
			{
                if(!mimeTypes.ContainsKey(mediaType.MediaType))
				    mimeTypes.Add(mediaType.MediaType, mediaType.MediaType == "application/json" ? mimeType : new MimeType());
			}
			
			return mimeTypes;
		}

	    protected abstract MimeType CreateMimeType(string type);


	    private IDictionary<string, Parameter> GetQueryParameters(IEnumerable<ApiParameterDescription> parameterDescriptions)
        {
            var queryParams = new Dictionary<string, Parameter>();

            foreach (var apiParam in parameterDescriptions.Where(p => p.Source == ApiParameterSource.FromUri))
            {
                if (apiParam.ParameterDescriptor == null)
                    continue;

                if (!IsPrimitiveType(apiParam.ParameterDescriptor.ParameterType))
                {
                    GetParametersFromComplexType(apiParam, queryParams);
                }
                else
                {
                    var parameter = GetPrimitiveParameter(apiParam);

                    if (!queryParams.ContainsKey(apiParam.Name))
                        queryParams.Add(apiParam.Name, parameter);
                }
            }
            return queryParams;
        }

	    private void GetParametersFromComplexType(ApiParameterDescription apiParam, IDictionary<string, Parameter> queryParams)
	    {
	        var properties = apiParam.ParameterDescriptor.ParameterType
	            .GetProperties().Where(p => p.CanWrite);
	        foreach (var property in properties)
	        {
                if(!IsPrimitiveType(property.PropertyType))
                    continue;

	            var parameter = GetParameterFromProperty(apiParam, property);

	            if (!queryParams.ContainsKey(property.Name))
	                queryParams.Add(property.Name, parameter);
	        }
	    }

	    private static Parameter GetParameterFromProperty(ApiParameterDescription apiParam, PropertyInfo property)
	    {
	        var parameter = new Parameter
	        {
	            Default = IsNullable(property.PropertyType) ? "null" : null,
	            Required = !IsNullable(property.PropertyType),
	            Type = SchemaTypeMapper.Map(property.PropertyType)
	        };
	        return parameter;
	    }

	    private static Parameter GetPrimitiveParameter(ApiParameterDescription apiParam)
	    {
	        var parameter = new Parameter
	        {
	            Default =
	                apiParam.ParameterDescriptor.DefaultValue == null
	                    ? (apiParam.ParameterDescriptor.IsOptional ? "null" : null)
	                    : apiParam.ParameterDescriptor.DefaultValue.ToString(),
	            Required = !apiParam.ParameterDescriptor.IsOptional,
	            Type = SchemaTypeMapper.Map(apiParam.ParameterDescriptor.ParameterType),
	            Description = apiParam.Documentation
	        };
	        return parameter;
	    }

	    private static bool IsNullable(Type type)
	    {
            return type == typeof(string) || Nullable.GetUnderlyingType(type) != null || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
	    }

	    private bool IsPrimitiveType(Type parameterType)
	    {
	        return SchemaTypeMapper.Map(parameterType) != null;
	    }

		private IDictionary<string, Parameter> GetUriParameters(string url, IEnumerable<ApiParameterDescription> apiParameterDescriptions)
		{
			var urlParameters = GetParametersFromUrl(url);
			var parameterDescriptions = apiParameterDescriptions
				.Where(apiParam => urlParameters.ContainsKey(apiParam.Name) 
					&& !string.IsNullOrWhiteSpace(apiParam.Documentation));

			foreach (var apiParam in parameterDescriptions)
			{
				urlParameters[apiParam.Name].Description = apiParam.Documentation;
			}
			return urlParameters;
		}

		private static IDictionary<string, Parameter> GetParametersFromUrl(string url)
		{
			var dic = new Dictionary<string, Parameter>();

			if (string.IsNullOrWhiteSpace(url) || !url.Contains("{"))
				return dic;

			if (!url.Contains("{"))
				return dic;

			var regex = new Regex("{([^}]+)}");
			var matches = regex.Matches(url);
			foreach (Match match in matches)
			{
				var parameter = new Parameter {Required = true, Type = "string"};
				dic.Add(match.Groups[1].Value, parameter);
			}

			return dic;
		}
	}
}
