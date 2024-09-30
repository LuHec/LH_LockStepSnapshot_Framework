using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using StargateNet;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace StargateNet
{
    public class SgNetworkILProcessor : ILPostProcessor
    {
        private const string SgNetworkAsmdefName = "Unity.SgNetwork";

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            // 筛选出引用了或者本身就是SgNetwork dll的程序集
            bool relevant = compiledAssembly.Name == SgNetworkAsmdefName ||
                            compiledAssembly.References.Any(filePath =>
                                Path.GetFileNameWithoutExtension(filePath) == SgNetworkAsmdefName);
            return relevant;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!this.WillProcess(compiledAssembly)) return new ILPostProcessResult(null!);
            var loader = new AssemblyResolver();

            var folders = new HashSet<string>();
            foreach (var reference in compiledAssembly.References)
                folders.Add(Path.Combine(Environment.CurrentDirectory, Path.GetDirectoryName(reference)));

            var folderList = folders.OrderBy(x => x);
            foreach (var folder in folderList) loader.AddSearchDirectory(folder);

            var readerParameters = new ReaderParameters
            {
                InMemory = true,
                AssemblyResolver = loader,
                ReadSymbols = true,
                ReadingMode = ReadingMode.Deferred
            };

            // 读入符号表
            readerParameters.SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData);

            // 读入目标程序集定义
            var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData),
                readerParameters);

            // 处理程序集，注入代码
            List<DiagnosticMessage> diagnostics = new();
            // 处理SyncVar标记
            diagnostics.AddRange(NetworkedAttributeProcessor.ProcessAssembly(assembly));
            // throw new Exception("Break");
            // 重新写回
            byte[] peData;
            byte[] pdbData;
            {
                var peStream = new MemoryStream();
                var pdbStream = new MemoryStream();
                var writeParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    WriteSymbols = true,
                    SymbolStream = pdbStream
                };

                assembly.Write(peStream, writeParameters);
                peStream.Flush();
                pdbStream.Flush();

                peData = peStream.ToArray();
                pdbData = pdbStream.ToArray();
            }

            return new ILPostProcessResult(new InMemoryAssembly(peData, pdbData), diagnostics);
        }
    }
}