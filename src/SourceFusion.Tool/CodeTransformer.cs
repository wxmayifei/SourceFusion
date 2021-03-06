﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using dotnetCampus.SourceFusion.CompileTime;

namespace dotnetCampus.SourceFusion
{
    /// <summary>
    /// 为编译时提供源码转换。
    /// </summary>
    internal class CodeTransformer
    {
        static CodeTransformer()
        {
            const string toolName = "SourceFusion";
            var toolVersion = Assembly.GetEntryAssembly().GetName().Version;

            GeneratedHeader =
                $@"//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:{Environment.Version.ToString(4)}
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

#define GENERATED_CODE

";
            GeneratedAttribute = $"[System.CodeDom.Compiler.GeneratedCode(\"{toolName}\", \"{toolVersion}\")]";
        }

        /// <summary>
        /// 创建用于转换源码的 <see cref="CodeTransformer"/>。
        /// </summary>
        /// <param name="workingFolder">转换源码的工作路径。</param>
        /// <param name="generatedCodeFolder">中间文件的生成路径（文件夹，相对路径）。</param>
        /// <param name="assembly">需要分析源码转换的程序集。</param>
        internal CodeTransformer(string workingFolder, string generatedCodeFolder, CompileAssembly assembly)
        {
            _workingFolder = workingFolder;
            _generatedCodeFolder = generatedCodeFolder;
            _assembly = assembly;
        }

        /// <summary>
        /// 获取可以为每一个生成的文件都增加的文件头。
        /// </summary>
        private static readonly string GeneratedHeader;

        /// <summary>
        /// 如果需要为类加上 <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/>，则使用此字符串。
        /// </summary>
        private static readonly string GeneratedAttribute;

        /// <summary>
        /// 获取编译期的程序集。
        /// </summary>
        private readonly CompileAssembly _assembly;

        /// <summary>
        /// 获取中间文件的生成路径（文件夹，相对路径）。
        /// </summary>
        private readonly string _generatedCodeFolder;

        /// <summary>
        /// 获取转换源码的工作路径。
        /// </summary>
        private readonly string _workingFolder;

        /// <summary>
        /// 执行 <see cref="IPlainCodeTransformer"/> 的转换代码的方法。
        /// </summary>
        /// <param name="codeFile">此代码文件的文件路径。</param>
        /// <param name="transformer">编译好的代码转换类实例。</param>
        private IEnumerable<string> InvokeCodeTransformer(string codeFile, IPlainCodeTransformer transformer)
        {
            var attribute = transformer.GetType().GetCustomAttribute<CompileTimeCodeAttribute>();

            var sourceFiles = attribute.SourceFiles
                .Select(x => Path.GetFullPath(Path.Combine(x.StartsWith("/") || x.StartsWith("\\")
                    ? _workingFolder
                    : Path.GetDirectoryName(codeFile), x)));
            foreach (var sourceFile in sourceFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(sourceFile);
                var extension = attribute.TargetType == FileType.Compile ? ".cs" : Path.GetExtension(sourceFile);

                var text = File.ReadAllText(sourceFile);
                for (var i = 0; i < attribute.RepeatCount; i++)
                {
                    var transformedText = transformer.Transform(text, new TransformingContext(i));
                    var targetFile = Path.Combine(_generatedCodeFolder, $"{fileName}.g.{i}{extension}");
                    File.WriteAllText(targetFile, GeneratedHeader + transformedText, Encoding.UTF8);
                }

                if (!attribute.KeepSourceFiles)
                {
                    yield return sourceFile;
                }
            }
        }

        /// <summary>
        /// 执行代码转换。这将开始从所有的编译文件中搜索 <see cref="CompileTimeCodeAttribute"/>，并执行其转换方法。
        /// </summary>
        internal IEnumerable<string> Transform()
        {
            foreach (var assemblyFile in _assembly.Files)
            {
                var compileType = assemblyFile.Types.FirstOrDefault();
                if
                (
                    compileType != null
                    && compileType.Attributes
                        .Any(x => x.Match<CompileTimeCodeAttribute>())
                )
                {
                    var type = assemblyFile.Compile().First();
                    var transformer = (IPlainCodeTransformer) Activator.CreateInstance(type);
                    var excludedFiles = InvokeCodeTransformer(assemblyFile.FullName, transformer);
                    yield return assemblyFile.FullName;
                    foreach (var excludedFile in excludedFiles)
                    {
                        yield return excludedFile;
                    }
                }
            }
        }
    }
}