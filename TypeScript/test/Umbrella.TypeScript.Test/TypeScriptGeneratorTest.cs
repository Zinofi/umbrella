using System;
using System.Runtime.Loader;
using System.Reflection;
using System.Collections.Generic;
using Xunit;
using System.ComponentModel.DataAnnotations;

namespace Umbrella.TypeScript.Test
{
    public class TypeScriptGeneratorTest
    {
        [Fact]
        public void GenerateAllTest()
        {
            var testAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Umbrella.TypeScript.Test"));

            TypeScriptGenerator generator = new TypeScriptGenerator(new List<Assembly> { testAssembly })
                .IncludeStandardGenerators()
                .IncludeKnockoutGenerators();

            string output = generator.GenerateAll(true, true, TypeScriptPropertyMode.Model);

            Assert.NotNull(output);
        }

		[Fact]
		public void GenerateAllUsingOnlyNamedAssembliesTest()
		{
			var testAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Umbrella.TypeScript.Test"));

			TypeScriptGenerator generator = new TypeScriptGenerator("Umbrella.TypeScript.Test")
				.IncludeStandardGenerators()
				.IncludeKnockoutGenerators();

			string output = generator.GenerateAll(true, true, TypeScriptPropertyMode.Model);

			Assert.NotNull(output);
		}
	}

    [TypeScriptEnum]
    public enum TestEnum
    {
        None,
        Value1,
        Value2
    }

    [TypeScriptModel(TypeScriptOutputModelType.Class | TypeScriptOutputModelType.KnockoutClass)]
    public class TestClass : ITestInterface
    {
        [Range(10, 200)]
        public int TestInt { get; set; } = 100;
        public string TestString { get; set; }
        [EmailAddress]
        public string EmailAddress { get; set; }

        [EmailAddress]
        [Compare(nameof(EmailAddress), ErrorMessage = "Emails must match")]
        public string ConfirmEmailAddress { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    [TypeScriptModel(TypeScriptOutputModelType.Class | TypeScriptOutputModelType.KnockoutClass)]
    public class TestChildClass
    {
        public int? TestChildInt { get; set; }
        public TestEnum TestChildEnum { get; set; }

        [TypeScriptIgnore]
        public int IgnoreMe { get; set; }

        [TypeScriptNull]
        [Required]
        public string IHaveADefaultButAmForcedToBeNull { get; set; } = "I should not be output!";

        [TypeScriptEmpty]
        public List<string> TestStringList { get; set; }

        [TypeScriptOverride(typeof(string))]
        public List<int> TestIntList { get; set; } = new List<int>();
    }

    [TypeScriptModel(TypeScriptOutputModelType.Interface | TypeScriptOutputModelType.KnockoutInterface)]
    public interface ITestInterface
    {
        DateTime CreatedDate { get; }
    }
}