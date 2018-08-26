using System.Reflection;
using Inedo.Extensibility;

[assembly: AssemblyTitle("Kubernetes")]
[assembly: AssemblyDescription("Provides operations that interact with Kubernetes clusters.")]
[assembly: AssemblyCompany("Inedo, LLC.")]
[assembly: AssemblyCopyright("Copyright © Inedo 2018")]
[assembly: AssemblyProduct("any")]

// Not for ProGet
[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter | InedoProduct.Hedgehog)]

[assembly: ScriptNamespace("Kubernetes")]

[assembly: AssemblyVersion("1.0.0")]
[assembly: AssemblyFileVersion("1.0.0")]
