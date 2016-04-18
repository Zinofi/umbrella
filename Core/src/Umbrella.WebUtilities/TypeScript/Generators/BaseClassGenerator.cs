﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Extensions;
using Umbrella.WebUtilities.TypeScript.Attributes;
using Umbrella.WebUtilities.TypeScript.Enumerations;
using Umbrella.WebUtilities.TypeScript.Generators.Interfaces;

namespace Umbrella.WebUtilities.TypeScript.Generators
{
    public abstract class BaseClassGenerator : BaseGenerator
    {
        protected abstract TypeScriptOutputModelType InterfaceModelType { get; }

        protected override void WriteStart(Type modelType, StringBuilder builder)
        {
            string generatedName = TypeScriptUtility.GenerateTypeName(modelType.Name, modelType, OutputModelType);

            List<string> lstInterface = TypeScriptUtility.GetInterfaceNames(modelType, TypeScriptOutputModelType.Interface, true);

            if (lstInterface.Contains(generatedName))
            {
                //List of interfaces already contains the name - strip the leading 'I' from the generatedName
                generatedName = generatedName.TrimStart('I');
            }

            builder.Append($"\texport class {generatedName}");

            if (lstInterface.Count > 0)
                builder.Append(string.Format(" implements {0}", string.Join(", ", lstInterface)));

            builder.AppendLine();
            builder.AppendLine("\t{");
        }

        protected override void WriteProperty(TypeScriptMemberInfo tsInfo, StringBuilder builder)
        {
            if (!string.IsNullOrEmpty(tsInfo.TypeName))
                builder.AppendLine($"\t\t{tsInfo.Name}: {tsInfo.TypeName} = {tsInfo.InitialOutputValue};");
        }
    }
}