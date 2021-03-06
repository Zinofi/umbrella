﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbrella.Utilities.Extensions;
using Umbrella.TypeScript.Generators.Interfaces;

namespace Umbrella.TypeScript.Generators
{
    public class KnockoutInterfaceGenerator : BaseInterfaceGenerator
    {
        public override TypeScriptOutputModelType OutputModelType => TypeScriptOutputModelType.KnockoutInterface;

        protected override void WriteProperty(TypeScriptMemberInfo tsInfo, StringBuilder builder)
        {
            if (!string.IsNullOrEmpty(tsInfo.TypeName))
            {
                string strStrictNullCheck = StrictNullChecks && (tsInfo.IsNullable || PropertyMode == TypeScriptPropertyMode.Null) ? " | null" : "";

                string formatString = "\t\t{0}: ";

                if (tsInfo.TypeName.EndsWith("[]"))
                {
                    formatString += "KnockoutObservableArray<{1}>;";
                    tsInfo.TypeName = tsInfo.TypeName.TrimEnd('[', ']');
                }
                else
                {
                    formatString += "KnockoutObservable<{1}>;";
                }

                builder.AppendLine(string.Format(formatString, tsInfo.Name, $"{tsInfo.TypeName}{strStrictNullCheck}"));
            }
        }
    }
}
