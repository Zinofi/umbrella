﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbrella.TypeScript.Exceptions;
using Umbrella.TypeScript.Generators;
using Umbrella.TypeScript.Generators.Abstractions;
using Umbrella.Utilities.Comparers;
using Umbrella.Utilities.Extensions;

namespace Umbrella.TypeScript
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// This generator does not currently handle non-user types that are not marked with the TypeScriptModelAttribute
	/// i.e. a type that is part of the .NET framework other than a primitive, DateTime or string, array or IEnumerable
	/// </remarks>
	public class TypeScriptGenerator
	{
		#region Private Members
		private readonly List<Type> m_Types;
		private bool m_StrictNullChecks;
		private TypeScriptPropertyMode m_TypeScriptPropertyMode;
		#endregion

		#region Public Properties		
		/// <summary>
		/// Gets the generators collection. Each .NET type marked for generation will be passed through each generator.
		/// </summary>
		public HashSet<IGenerator> Generators { get; } = new HashSet<IGenerator>(new GenericEqualityComparer<IGenerator, Type>(x => x.GetType()));
		#endregion

		#region Public Methods

		/// <summary>
		/// Create a new <see cref="TypeScriptGenerator"/> instance.
		/// </summary>
		/// <param name="onlyNamedAssemblies">
		/// A list of assembly names to scan for <see cref="TypeScriptModelAttribute"/> declarations.
		/// If no names are specified then all assemblies in the current <see cref="AppDomain"/> will be loaded
		/// and scanned.
		/// </param>
		public TypeScriptGenerator(params string[] onlyNamedAssemblies)
		{
			try
			{
				IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies();

				if (onlyNamedAssemblies?.Length > 0)
					assemblies = assemblies.Where(x => onlyNamedAssemblies.Contains(x.GetName().Name));

				m_Types = assemblies.SelectMany(a => a.GetTypes()).ToList();
			}
			catch (ReflectionTypeLoadException exc)
			{
				var messageBuilder = new StringBuilder("There has been a problem loading the specified assemblies. The following loader exception messages have been encountered:")
					.AppendLine()
					.AppendLine();

				if (exc.LoaderExceptions?.Length > 0)
				{
					foreach (string message in exc.LoaderExceptions.Select(x => x.Message))
					{
						if (!string.IsNullOrWhiteSpace(message))
							messageBuilder.AppendLine("\t - " + message);
					}
				}

				throw new UmbrellaTypeScriptException(messageBuilder.ToString(), exc);
			}
			catch (Exception exc)
			{
				throw new UmbrellaTypeScriptException("There has been a problem loading the TypeScript generator.", exc);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeScriptGenerator"/> class.
		/// </summary>
		/// <param name="assemblies">The assemblies to scan for types to generate.</param>
		public TypeScriptGenerator(List<Assembly> assemblies)
		{
			m_Types = assemblies.SelectMany(a => a.GetTypes()).ToList();
		}

		/// <summary>
		/// Includes the standard generators.
		/// </summary>
		/// <returns>The current <see cref="TypeScriptGenerator" /> instance.</returns>
		public TypeScriptGenerator IncludeStandardGenerators()
		{
			Generators.Add(new StandardInterfaceGenerator());
			Generators.Add(new StandardClassGenerator());

			return this;
		}

		/// <summary>
		/// Includes the Knockout generators.
		/// </summary>
		/// <param name="useDecorators">Specifies whether Knockout specific EcmaScript decorators will be used when generating the properties on models or done using the standard ko.observable method calls.</param>
		/// <returns>The current <see cref="TypeScriptGenerator" /> instance.</returns>
		public TypeScriptGenerator IncludeKnockoutGenerators(bool useDecorators)
		{
			Generators.Add(new KnockoutInterfaceGenerator(useDecorators));
			Generators.Add(new KnockoutClassGenerator(useDecorators));

			return this;
		}

		/// <summary>
		/// Includes the specified generator type.
		/// </summary>
		/// <typeparam name="T">The generator type.</typeparam>
		/// <returns>The current <see cref="TypeScriptGenerator" /> instance.</returns>
		public TypeScriptGenerator IncludeGenerator<T>()
			where T : IGenerator, new()
		{
			Generators.Add(new T());

			return this;
		}

		/// <summary>
		/// Generates all models.
		/// </summary>
		/// <param name="outputAsModuleExport">if set to <c>true</c> outputs the generated module using 'export module' instead of 'namespace'.</param>
		/// <param name="strictNullChecks">if set to <c>true</c>, enables strict null checks, e.g. '| null'</param>
		/// <param name="propertyMode">The property mode.</param>
		/// <returns>The generated TypeScript module containing all generated TypeScript interfaces and classes.</returns>
		public string GenerateAll(bool outputAsModuleExport = true, bool strictNullChecks = true, TypeScriptPropertyMode propertyMode = TypeScriptPropertyMode.Model)
		{
			m_StrictNullChecks = strictNullChecks;
			m_TypeScriptPropertyMode = propertyMode;

			var sbNamespaces = new StringBuilder();

			//Before processing the models, firstly find all the enums that need to be generated
			var enumItems = GetEnumItems().ToList();
			var enumGroups = GetEnumItems().GroupBy(x => x.Namespace).ToDictionary(x => x.Key, x => x.ToList());

			//Start of TypeScript namespace or module export
			string namespaceOrModuleStart = outputAsModuleExport
				? "export module"
				: "namespace";

			//Generate the models
			foreach (var group in GetModelItems().GroupBy(x => x.ModelType.Namespace))
			{
				string nsName = group.Key;

				sbNamespaces.AppendLine($"{namespaceOrModuleStart} {nsName}")
					.AppendLine("{");

				//Generate enum definitions for this namespace if any exist
				if (enumGroups.ContainsKey(nsName))
				{
					List<Type> lstEnumToGenerate = enumGroups[nsName];

					foreach (Type enumType in lstEnumToGenerate)
					{
						string enumOutput = GenerateEnumDefinition(enumType);

						sbNamespaces.AppendLine(enumOutput);
					}

					//Remove this key from the dictionary seeing as it has now been processed
					enumGroups.Remove(nsName);
				}

				// Generate model interfaces and classes
				foreach (TypeScriptModelGeneratorItem item in group)
				{
					//Generate the models using the registered generators
					foreach (IGenerator generator in Generators)
					{
						TypeScriptModelAttribute attribute = item.ModelAttribute;

						if (attribute.OutputModelTypes.HasFlag(generator.OutputModelType))
						{
							string generatorOutput = generator.Generate(item.ModelType, attribute.GenerateValidationRules, m_StrictNullChecks, m_TypeScriptPropertyMode);

							sbNamespaces.AppendLine(generatorOutput);
						}
					}
				}

				//End of TypeScript namespace
				sbNamespaces.AppendLine("}");
			}

			//Now generate enums in namespaces that couldn't be placed within the same namespace as any of the generated models
			foreach (var group in enumGroups)
			{
				//Start of TypeScript namespace
				sbNamespaces.AppendLine($"{namespaceOrModuleStart} {group.Key}")
					.AppendLine("{");

				foreach (Type enumType in group.Value)
				{
					string enumOutput = GenerateEnumDefinition(enumType);

					sbNamespaces.AppendLine(enumOutput);
				}

				//End of TypeScript namespace
				sbNamespaces.AppendLine("}");
			}

			return sbNamespaces.ToString();
		}
		#endregion

		#region Private Methods
		private string GenerateEnumDefinition(Type enumType)
		{
			var builder = new StringBuilder();
			builder.AppendLine($"\texport enum {enumType.Name}");
			builder.AppendLine("\t{");

			foreach (var enumItem in enumType.GetEnumDictionary())
			{
				builder.AppendLine($"\t\t{enumItem.Value} = {enumItem.Key},");
			}

			builder.AppendLine("\t}");
			return builder.ToString();
		}

		private IEnumerable<TypeScriptModelGeneratorItem> GetModelItems()
		{
			foreach (Type type in m_Types)
			{
				TypeScriptModelAttribute modelAttribute = type.GetCustomAttribute<TypeScriptModelAttribute>();

				if (modelAttribute == null)
					continue;

				yield return new TypeScriptModelGeneratorItem { ModelType = type, ModelAttribute = modelAttribute };
			}
		}

		private IEnumerable<Type> GetEnumItems()
		{
			foreach (var type in m_Types)
			{
				if (!type.IsEnum)
					continue;

				TypeScriptEnumAttribute enumAttribute = type.GetCustomAttribute<TypeScriptEnumAttribute>();

				if (enumAttribute == null)
					continue;

				yield return type;
			}
		}
		#endregion
	}
}