﻿using Microsoft.CodeAnalysis;
using ProtoBuf.BuildTools;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BuildToolsUnitTests
{
    public class ProtobufFieldAnalyzerTests : AnalyzerTestBase<ProtoBufFieldAnalyzer>
    {
        public ProtobufFieldAnalyzerTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Fact]
        public async Task DoesntReportOnUnrelatedCode()
        {
            var diagnostics = await AnalyzeAsync(@"
public class Foo
{
    public void Bar() {}
}");
            Assert.Empty(diagnostics);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(18999)]
        [InlineData(20000)]
        [InlineData(536870911)]
        // The smallest field number you can specify is 1, and the largest is 229 - 1, or 536,870,911. You also cannot use the numbers 19000 through 19999
        // from https://developers.google.com/protocol-buffers/docs/proto3#assigning_field_numbers
        public async Task DoesntReportOnLegalDto(int fieldNumber)
        {
            var diagnostics = await AnalyzeAsync($@"
using ProtoBuf;
[ProtoContract]
public class Foo
{{
    [ProtoMember({fieldNumber})]
    public int Bar {{get;set;}}
}}");
            Assert.Empty(diagnostics);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(42)]
        [InlineData(18999)]
        [InlineData(20000)]
        [InlineData(536870911)]
        // The smallest field number you can specify is 1, and the largest is 229 - 1, or 536,870,911. You also cannot use the numbers 19000 through 19999
        // from https://developers.google.com/protocol-buffers/docs/proto3#assigning_field_numbers
        public async Task DoesntReportOnLegalDto_Partial(int fieldNumber)
        {
            var diagnostics = await AnalyzeAsync($@"
using ProtoBuf;
[ProtoContract]
[ProtoPartialMember({fieldNumber}, nameof(Bar))]
public class Foo
{{
    public int Bar {{get;set;}}
}}");
            Assert.Empty(diagnostics);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-42)]
        [InlineData(19000, true)]
        [InlineData(19500, true)]
        [InlineData(19999, true)]
        [InlineData(536870912)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        // The smallest field number you can specify is 1, and the largest is 229 - 1, or 536,870,911. You also cannot use the numbers 19000 through 19999
        // from https://developers.google.com/protocol-buffers/docs/proto3#assigning_field_numbers
        public async Task ReportsOnIllegalDto(int fieldNumber, bool warningOnly = false)
        {
            var diagnostics = await AnalyzeAsync($@"
using ProtoBuf;
[ProtoContract]
public class Foo
{{
    [ProtoMember({fieldNumber})]
    public int Bar {{get;set;}}
}}");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.InvalidFieldNumber);
            Assert.Equal(warningOnly ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The specified field number {fieldNumber} is invalid; the valid range is 1-536870911, omitting 19000-19999.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsOnIllegalPartialName()
        {
            var diagnostics = await AnalyzeAsync($@"
using ProtoBuf;
[ProtoContract]
[ProtoPartialMember(42, ""Bar"")]
public class Foo
{{
    public int Blap {{get;set;}}
}}");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.MemberNotFound);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal("The specified type member 'Bar' could not be resolved.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-42)]
        [InlineData(19000, true)]
        [InlineData(19500, true)]
        [InlineData(19999, true)]
        [InlineData(536870912)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        // The smallest field number you can specify is 1, and the largest is 229 - 1, or 536,870,911. You also cannot use the numbers 19000 through 19999
        // from https://developers.google.com/protocol-buffers/docs/proto3#assigning_field_numbers
        public async Task ReportsOnIllegalDto_Partial(int fieldNumber, bool warningOnly = false)
        {
            var diagnostics = await AnalyzeAsync($@"
using ProtoBuf;
[ProtoContract]
[ProtoPartialMember({fieldNumber}, nameof(Bar))]
public class Foo
{{
    public int Bar {{get;set;}}
}}");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.InvalidFieldNumber);
            Assert.Equal(warningOnly ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The specified field number {fieldNumber} is invalid; the valid range is 1-536870911, omitting 19000-19999.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsOnIllegalConst()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
public class Foo
{
    private const int FieldNumber = -42;
    [ProtoMember(FieldNumber)]
    public int Bar {get;set;}
}");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.InvalidFieldNumber);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The specified field number -42 is invalid; the valid range is 1-536870911, omitting 19000-19999.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateFieldBetweenFields()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
public class Foo
{
    [ProtoMember(1)]
    public int A {get;set;}

    [ProtoMember(2)]
    public int B {get;set;}

    [ProtoMember(2)]
    public int C {get;set;}

    [ProtoMember(3)]
    public int D {get;set;}
}");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateFieldNumber);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The specified field number 2 is duplicated; field numbers must be unique between all declared members and includes on a single type.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateFieldBetweenPartialsAndIncludes()
        {
            // note we don't need to test all combinations of fields vs partials vs includes; it is all one bucket - it
            // is sufficient that it *finds the problem*
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
[ProtoInclude(3, typeof(SuperFoo))]
[ProtoPartialMember(3, ""C"")]
public class Foo
{
    [ProtoMember(1)]
    public int A {get;set;}

    [ProtoMember(2)]
    public int B {get;set;}

    public int C {get;set;}
}

[ProtoContract]
public class SuperFoo : Foo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateFieldNumber);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The specified field number 3 is duplicated; field numbers must be unique between all declared members and includes on a single type.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsShouldBeContract_Member()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
public class Foo
{
    [ProtoMember(1)]
    public int A {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ShouldBeProtoContract);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The type is not marked as a proto-contract; additional annotations will be ignored.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsShouldBeContract_Include()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoInclude(typeof(SuperFoo))]
public class Foo
{
    [ProtoMember(1)]
    public int A {get;set;}
}
[ProtoContract]
public class SuperFoo : Foo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ShouldBeProtoContract);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The type is not marked as a proto-contract; additional annotations will be ignored.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsShouldBeContract_Reservation()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoReserved(123)]
public class Foo
{
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ShouldBeProtoContract);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The type is not marked as a proto-contract; additional annotations will be ignored.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsBothDeclaredAndIgnored()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoPartialIgnore(""A"")]
public class Foo
{
    [ProtoMember(1)]
    public int A {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DeclaredAndIgnored);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The member 'A' is marked to be ignored; additional annotations will be ignored.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsReservedField_Number()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoReserved(42)]
public class Foo
{
    [ProtoMember(42)]
    public int A {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ReservedFieldNumber);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The specified field number 42 is explicitly reserved.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsReservedField_Number_Range()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoReserved(40,50)]
public class Foo
{
    [ProtoMember(42)]
    public int A {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ReservedFieldNumber);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The specified field number [40-50] is explicitly reserved.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsReservedField_Name()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoReserved(""A"")]
public class Foo
{
    [ProtoMember(42)]
    public int A {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ReservedFieldName);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The specified field name 'A' is explicitly reserved.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateName()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
public class Foo
{
    [ProtoMember(1, Name = ""C"")]
    public int A {get;set;}
    [ProtoMember(2, Name = ""C"")]
    public int B {get;set;}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateFieldName);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The specified field name 'C' is duplicated; field names should be unique between all declared members on a single type.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateReservationName()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoReserved(""A""), ProtoReserved(""A"")]
public class Foo
{
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateReservation);
            Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
            Assert.Equal($"The reservations 'A' and 'A' overlap each-other.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateReservationNumbers()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoReserved(10, 25), ProtoReserved(20, 30)]
public class Foo
{
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateReservation);
            Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
            Assert.Equal($"The reservations [20-30] and [10-25] overlap each-other.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsDuplicateInclude()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
[ProtoInclude(1, typeof(SuperFoo))]
[ProtoInclude(2, typeof(SuperFoo))]
public class Foo
{
}
[ProtoContract]
public class SuperFoo : Foo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.DuplicateInclude);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The type 'SuperFoo' is declared as an include multiple times.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsNonDerivedInclude()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
[ProtoInclude(1, typeof(SuperFoo))]
public class Foo
{
}
[ProtoContract]
public class SuperFoo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.IncludeNonDerived);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"The type 'SuperFoo' is declared as an include, but is not a direct sub-type.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsMissingIncludeDeclaration()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
public class Foo {}
[ProtoContract]
public class SuperFoo : Foo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.IncludeNotDeclared);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The base-type 'Foo' is a proto-contract, but no include is declared for 'SuperFoo' and the IgnoreUnknownSubTypes flag is not set.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task DoesNotReportMissingIncludeDeclarationWhenIgnoreSubTypesSpecified()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract(IgnoreUnknownSubTypes = true)]
public class Foo {}
public class SuperFoo : Foo {}
");
            Assert.Empty(diagnostics);
        }

        [Fact]
        public async Task ReportsIncludeNotContract()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract, ProtoInclude(3, typeof(SuperFoo))]
public class Foo {}
public class SuperFoo : Foo {}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.SubTypeShouldBeProtoContract);
            Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
            Assert.Equal($"The base-type 'Foo' is a proto-contract and the IgnoreUnknownSubTypes flag is not set; 'SuperFoo' should also be a proto-contract.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsMissingConstructor()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract]
public class Foo {
    public Foo(string _) {}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ConstructorMissing);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"There is no suitable (parameterless) constructor available for the proto-contract, and the SkipConstructor flag is not set.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task ReportsMissingConstructor_ExplicitNoSkip()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract(SkipConstructor = false)]
public class Foo {
    public Foo(string _) {}
}
");
            var diag = Assert.Single(diagnostics, x => x.Descriptor == ProtoBufFieldAnalyzer.ConstructorMissing);
            Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
            Assert.Equal($"There is no suitable (parameterless) constructor available for the proto-contract, and the SkipConstructor flag is not set.", diag.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task DoesNotReportsMissingConstructor_ExplicitSkip()
        {
            var diagnostics = await AnalyzeAsync(@"
using ProtoBuf;
[ProtoContract(SkipConstructor = true)]
public class Foo {
    public Foo(string _) {}
}
");
            Assert.Empty(diagnostics);
        }
    }
}
