﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Xunit;
using Cci = Microsoft.Cci;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public static class CompilationExtensions
    {
        internal static ImmutableArray<byte> EmitToArray(
            this Compilation compilation, 
            bool metadataOnly = false,
            bool debug = false,
            CompilationTestData testData = null, 
            DiagnosticDescription[] expectedWarnings = null)
        {
            var stream = new MemoryStream();

            var emitResult = compilation.Emit(
                executableStream: stream,
                outputName: null,
                pdbFilePath: debug ? "Compilation.pdb" : null,
                pdbStream: debug ? new MemoryStream() : null,
                xmlDocStream: null,
                cancellationToken: default(CancellationToken),
                win32Resources: null,
                manifestResources: null,
                metadataOnly: metadataOnly,
                testData: testData);

            Assert.True(emitResult.Success, "Diagnostics: " + string.Join(", ", emitResult.Diagnostics.Select(d => d.ToString())));

            if (expectedWarnings != null)
            {
                emitResult.Diagnostics.Verify(expectedWarnings);
            }

            return stream.ToImmutable();
        }

        public static Stream EmitToStream(this Compilation compilation, bool metadataOnly = false, DiagnosticDescription[] expectedWarnings = null)
        {
            var stream = new MemoryStream();
            var emitResult = metadataOnly ? compilation.EmitMetadataOnly(stream) : compilation.Emit(stream);
            Assert.True(emitResult.Success, "Diagnostics: " + string.Join(", ", emitResult.Diagnostics.Select(d => d.ToString())));

            if (expectedWarnings != null)
            {
                emitResult.Diagnostics.Verify(expectedWarnings);
            }

            stream.Position = 0;
            return stream;
        }


        public static MetadataReference EmitToImageReference(this Compilation comp, params DiagnosticDescription[] expectedWarnings)
        {
            var image = comp.EmitToArray(expectedWarnings: expectedWarnings);
            if (comp.Options.OutputKind.IsNetModule()) 
            {
                return new MetadataImageReference(ModuleMetadata.CreateFromImage(image), display: comp.MakeSourceModuleName());
            }
            else
            {
                return new MetadataImageReference(image, display: comp.MakeSourceAssemblySimpleName());
            }
        }

        internal static CompilationDifference EmitDifference(
            this Compilation compilation,
            EmitBaseline baseline,
            ImmutableArray<SemanticEdit> edits,
            CompilationTestData testData = null)
        {
            testData = testData ?? new CompilationTestData();
            var pdbName = Path.ChangeExtension(compilation.SourceModule.Name, "pdb");

            // keep the stream open, it's passed to CompilationDifference
            var pdbStream = new MemoryStream();

            using (MemoryStream mdStream = new MemoryStream(), ilStream = new MemoryStream())
            {
                var updatedMethodTokens = new List<uint>();

                var result = compilation.EmitDifference(
                    baseline,
                    edits,
                    mdStream,
                    ilStream,
                    pdbStream,
                    updatedMethodTokens,
                    testData,
                    default(CancellationToken));

                pdbStream.Seek(0, SeekOrigin.Begin);

                return new CompilationDifference(
                    mdStream.ToImmutable(),
                    ilStream.ToImmutable(),
                    pdbStream,
                    result.Baseline,
                    testData,
                    result);
            }
        }
    }
}
