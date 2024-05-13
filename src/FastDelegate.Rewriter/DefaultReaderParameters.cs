using Mono.Cecil;

namespace FastDelegate.Rewriter;

public static class DefaultReaderParameters
{
    public static readonly ReaderParameters Value = new ReaderParameters()
    {
        AssemblyResolver = new DefaultAssemblyResolver(),
        InMemory = true,
        ReadWrite = false,
        ReadingMode = ReadingMode.Immediate,
        MetadataImporterProvider = new SystemPrivateCoreLibFixerMetadataImporterProvider(),
        ReflectionImporterProvider = new SystemPrivateCoreLibFixerReflectionProvider()
    };
}