using System.Reflection;
using Inedo.Extensibility;

[assembly: AssemblyTitle("Kubernetes")]
[assembly: AssemblyDescription("Provides operations that interact with Kubernetes clusters.")]
[assembly: AssemblyCompany("Inedo, LLC.")]
[assembly: AssemblyCopyright("Copyright © Inedo 2021")]
[assembly: AssemblyProduct("any")]

[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter | InedoProduct.ProGet)]

[assembly: ScriptNamespace("Kubernetes")]

[assembly: AssemblyVersion("1.10.0")]
[assembly: AssemblyFileVersion("1.10.0")]
