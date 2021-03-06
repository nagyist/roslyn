﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeCleanup;
using Microsoft.CodeAnalysis.CodeCleanup.Providers;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.CodeCleanup
{
    public class AddMissingTokensTests
    {
        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MultipleLineIfStatementThen()
        {
            var code = @"[|
        If True
            Dim a = 1
        End If|]";

            var expected = @"
        If True Then
            Dim a = 1
        End If";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TypeArgumentOf()
        {
            var code = @"[|
        Dim a As List(Integer)|]";

            var expected = @"
        Dim a As List(Of Integer)";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TypeParameterOf()
        {
            var code = @"[|Class A(T)
End Class|]";

            var expected = @"Class A(Of T)
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodDeclaration()
        {
            var code = @"Class A
    [|Sub Test
    End Sub|]
End Class";

            var expected = @"Class A
    Sub Test()
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544318, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_TypeArgParens()
        {
            var code = @"[|Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading

Module Program
    Sub Main(args As String())
        [|Dim q = New List(Of Thread

    |]End Sub|]";

            var expected = @"Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading

Module Program
    Sub Main(args As String())
        Dim q = New List(Of Thread

    End Sub";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_Sub()
        {
            var code = @"Class A
    Sub Test
        [|Test|]
    End Sub
End Class";

            var expected = @"Class A
    Sub Test
        Test()
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_Function()
        {
            var code = @"Class A
    Function Test() As Integer
        [|Test|]
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Test()
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void IdentifierMethod_Return()
        {
            var code = @"Class A
    Function Test() As Integer
        Return [|Test2|]
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Return Test2()
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void IdentifierMethod_Assign()
        {
            var code = @"Class A
    Function Test() As Integer
        Dim a = [|Test2|]
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Dim a = Test2()
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void IdentifierMethod_DotName_DontAdd()
        {
            var code = @"Class A
    Function Test() As Integer
        Dim a = [|Me.Test2|]
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Dim a = Me.Test2
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_DotName()
        {
            var code = @"Class A
    Function Test() As Integer
        [|Me.Test2|]
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Me.Test2()
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_Generic()
        {
            var code = @"Class A
    Function Test() As Integer
        [|Me.Test2(Of Integer)|]
        Return 1
    End Function

    Function Test2(Of T) As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Me.Test2(Of Integer)()
        Return 1
    End Function

    Function Test2(Of T) As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_Call()
        {
            var code = @"Class A
    Function Test() As Integer
        Call [|Me.Test2|]
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Call Me.Test2()
        Return 1
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void EventHandler_AddressOf1()
        {
            var code = @"Class A
    Sub EventMethod()
        [|AddHandler TestEvent, AddressOf EventMethod|]
    End Sub

    Public Event TestEvent()
End Class";

            var expected = @"Class A
    Sub EventMethod()
        AddHandler TestEvent, AddressOf EventMethod
    End Sub

    Public Event TestEvent()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void EventHandler_AddressOf2()
        {
            var code = @"Class A
    Sub EventMethod()
        [|RemoveHandler TestEvent, AddressOf EventMethod|]
    End Sub

    Public Event TestEvent()
End Class";

            var expected = @"Class A
    Sub EventMethod()
        RemoveHandler TestEvent, AddressOf EventMethod
    End Sub

    Public Event TestEvent()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void Delegate_AddressOf()
        {
            var code = @"Class A
    Sub Method()
        [|Dim a As Action = New Action(AddressOf Method)|]
    End Sub
End Class";

            var expected = @"Class A
    Sub Method()
        Dim a As Action = New Action(AddressOf Method)
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void EventDeclaration()
        {
            var code = @"Class A
    Sub EventMethod()
        [|RaiseEvent TestEvent|]
    End Sub

    Public Event TestEvent()
End Class";

            var expected = @"Class A
    Sub EventMethod()
        RaiseEvent TestEvent()
    End Sub

    Public Event TestEvent()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void RaiseEvent()
        {
            var code = @"Class A
    Sub EventMethod()
        RaiseEvent TestEvent
    End Sub

    [|Public Event TestEvent|]
End Class";

            var expected = @"Class A
    Sub EventMethod()
        RaiseEvent TestEvent
    End Sub

    Public Event TestEvent()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DelegateInvocation()
        {
            var code = @"
        Dim a As Action
        [|a|]";

            var expected = @"
        Dim a As Action
        a()";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void Attribute()
        {
            var code = @"[|<Obsolete>
Class C(Of T)
    Sub Main
        Dim a = {1, 2, 3}
    End Sub
End Class|]";

            var expected = @"<Obsolete>
Class C(Of T)
    Sub Main()
        Dim a = {1, 2, 3}
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ObjectCreation()
        {
            var code = @"[|Module Program
    Sub Main(args As String())
        Dim x As New ClassInNewFile
        Dim c As CustomClass = New CustomClass(""constructor"")
    End Sub
End Module

Friend Class CustomClass
    Sub New(p1 As String)
    End Sub
End Class

Class ClassInNewFile
End Class|]";

            var expected = @"Module Program
    Sub Main(args As String())
        Dim x As New ClassInNewFile
        Dim c As CustomClass = New CustomClass(""constructor"")
    End Sub
End Module

Friend Class CustomClass
    Sub New(p1 As String)
    End Sub
End Class

Class ClassInNewFile
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void Constructor()
        {
            var code = @"[|Class C
    Sub New
    End Sub
End Class|]";

            var expected = @"Class C
    Sub New()
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DeclareStatement()
        {
            var code = @"[|Class C
    Declare Function getUserName Lib ""advapi32.dll"" Alias ""GetUserNameA"" 
End Class|]";

            var expected = @"Class C
    Declare Function getUserName Lib ""advapi32.dll"" Alias ""GetUserNameA"" ()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DelegateStatement()
        {
            var code = @"[|Class C
    Delegate Sub Test
End Class|]";

            var expected = @"Class C
    Delegate Sub Test()
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodStatementWithComment()
        {
            var code = @"[|Class C
    [|Sub Test ' test
    End Sub|]
End Class|]";

            var expected = @"Class C
    Sub Test() ' test
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MultipleLineIfStatementThenWithComment()
        {
            var code = @"[|
        If True ' test
            Dim a = 1
        End If|]";

            var expected = @"
        If True Then ' test
            Dim a = 1
        End If";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TypeArgumentOf_Comment_DontAdd()
        {
            var code = @"[|
        Dim a As List( ' test
                      Integer)|]";

            var expected = @"
        Dim a As List( ' test
                      Integer)";

            // parser doesn't recognize the broken list as Type Argument List
            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TypeParameterOf_Comment()
        {
            var code = @"[|Class A( ' test
                                   T)
End Class|]";

            var expected = @"Class A(Of ' test
                                   T)
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void MethodInvocation_Function_Comment()
        {
            var code = @"Class A
    Function Test() As Integer
        [|Test ' test|]
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Test() ' test
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void IdentifierMethod_Return_Comment()
        {
            var code = @"Class A
    Function Test() As Integer
        Return [|Test2 ' test|]
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            var expected = @"Class A
    Function Test() As Integer
        Return Test2() ' test
    End Function

    Function Test2() As Integer
        Return 1
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ImplementsClause()
        {
            var code = @"Class Program
    Implements I
    [|Public Sub Method() Implements I.Method
    End Sub|]
End Class

Interface I
    Sub Method()
End Interface";

            var expected = @"Class Program
    Implements I
    Public Sub Method() Implements I.Method
    End Sub
End Class

Interface I
    Sub Method()
End Interface";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void OperatorStatement()
        {
            var code = @"[|Public Structure abc
    Public Shared Operator And(ByVal x As abc, ByVal y As abc) As abc
        Dim r As New abc
        ' Insert code to calculate And of x and y.
        Return r
    End Operator
End Structure|]";

            var expected = @"Public Structure abc
    Public Shared Operator And(ByVal x As abc, ByVal y As abc) As abc
        Dim r As New abc
        ' Insert code to calculate And of x and y.
        Return r
    End Operator
End Structure";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void PropertyAndAccessorStatement()
        {
            var code = @"[|Class Class1
    Private propertyValue As String
    Public Property prop1() As String
        Get
            Return propertyValue
        End Get
        Set(ByVal value As String)
            propertyValue = value
        End Set
    End Property
End Class|]";

            var expected = @"Class Class1
    Private propertyValue As String
    Public Property prop1() As String
        Get
            Return propertyValue
        End Get
        Set(ByVal value As String)
            propertyValue = value
        End Set
    End Property
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void LambdaExpression()
        {
            var code = @"[|Class Class1
    Dim f as Action = Sub()
                      End Sub
End Class|]";

            var expected = @"Class Class1
    Dim f as Action = Sub()
                      End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544225, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void StructuredTrivia_Expression_DontCrash()
        {
            var code = @"[|#Const Foo1 = 1
#Const Foo2 = 2
#If Foo1 Then
#ElseIf Foo2 Then
#Else
#End If|]";
            var expected = @"#Const Foo1 = 1
#Const Foo2 = 2
#If Foo1 Then
#ElseIf Foo2 Then
#Else
#End If";
            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544169, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void EventStatement_AsClause()
        {
            var code = @"[|Imports System.ComponentModel
Class Foo
    Public Event PropertyChanged As PropertyChangedEventHandler
End Class|]";
            var expected = @"Imports System.ComponentModel
Class Foo
    Public Event PropertyChanged As PropertyChangedEventHandler
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544167, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void InvocationExpression_NoParentheseForPredefinedCastExpression()
        {
            var code = @"[|Class Program
    Sub Main(args As String())
        CInt(5)
    End Sub
End Class|]";
            var expected = @"Class Program
    Sub Main(args As String())
        CInt(5)
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544178, "DevDiv")]
        [WorkItem(544317, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ObjectCreationExpression()
        {
            var code = @"[|Class C
    Function F() As C
        Return New C
    End Function
End Class|]";
            var expected = @"Class C
    Function F() As C
        Return New C
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544317, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ObjectCreationExpression_Initializer()
        {
            var code = @"[|Public Class SomeClass
    Public foo As Integer

    Sub SomeSub()
        [|Dim c = New SomeClass With {.foo = 23}|]
    End Sub
End Class|]";
            var expected = @"Public Class SomeClass
    Public foo As Integer

    Sub SomeSub()
        Dim c = New SomeClass With {.foo = 23}
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544178, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ObjectCreationExpression_GenericName()
        {
            var code = @"[|Imports System

Module Program
    Sub Main(args As String())
        Dim q = New List(Of Integer
    End Sub
End Module|]";

            var expected = @"Imports System

Module Program
    Sub Main(args As String())
        Dim q = New List(Of Integer
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544178, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ObjectCreationExpression_AsNewClause()
        {
            var code = @"[|Class C
    Dim a As New C
End Class|]";
            var expected = @"Class C
    Dim a As New C
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544301, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ContinueStatement_While()
        {
            var code = @"Module M
    Sub S()
        [|While True
            Continue
        End While|]
    End Sub
End Module";
            var expected = @"Module M
    Sub S()
        While True
            Continue While
        End While
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544301, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ContinueStatement_For()
        {
            var code = @"Module M
    Sub S()
        [|For i = 1 to 10
            Continue
        Next|]
    End Sub
End Module";
            var expected = @"Module M
    Sub S()
        For i = 1 to 10
            Continue For
        Next
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544380, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void IfDirective()
        {
            var code = @"[|#If VBC_VER >= 9.0

Class C
End Class|]";
            var expected = @"#If VBC_VER >= 9.0 Then

Class C
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544386, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void NamedFieldInitializer()
        {
            var code = @"[|Class S
    Public Sub Foo()
    End Sub
    Property X
    Sub test()
        Dim x = New S With {.X = 0,.Foo}
    End Sub
End Class|]";
            var expected = @"Class S
    Public Sub Foo()
    End Sub
    Property X
    Sub test()
        Dim x = New S With {.X = 0, .Foo}
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544526, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DontCrash_ImplementsStatement()
        {
            var code = @"[|Class C
    Sub Main() 
        Implements IDisposable.Dispose
    End Sub
End Class|]";
            var expected = @"Class C
    Sub Main()
        Implements IDisposable.Dispose
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(544525, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void AccessorStatement_AddRemoveHandler_RaiseEvent()
        {
            var code = @"[|Class C
    Public Custom Event E1 As Action
        AddHandler
        End AddHandler
        RemoveHandler
        End RemoveHandler
        RaiseEvent
        End RaiseEvent
    End Event
End Class|]";
            var expected = @"Class C
    Public Custom Event E1 As Action
        AddHandler()
        End AddHandler
        RemoveHandler()
        End RemoveHandler
        RaiseEvent()
        End RaiseEvent
    End Event
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545176, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void CallStatement_Lambda()
        {
            var code = @"[|Module Program
    Sub Main()
        Call Sub() Console.WriteLine(1)
    End Sub
End Module|]";
            var expected = @"Module Program
    Sub Main()
        Call Sub() Console.WriteLine(1)
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545256, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void HandlesClauseItem_DontAddParenthese()
        {
            var code = @"[|Structure s1
    Sub Foo() Handles Me.Foo
 
    End Sub
End Structure|]";
            var expected = @"Structure s1
    Sub Foo() Handles Me.Foo

    End Sub
End Structure";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545380, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DontAddParentheseInForEachControlVariable()
        {
            var code = @"[|Module Module1
    Sub Main()
        For Each foo in {} 
    End Sub
 
    Sub Foo()
    End Sub
End Module|]";
            var expected = @"Module Module1
    Sub Main()
        For Each foo in {}
    End Sub

    Sub Foo()
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545380, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DontAddParentheseInForControlVariable()
        {
            var code = @"[|Module Module1
    Sub Main()
        For foo to 
    End Sub
 
    Sub Foo()
    End Sub
End Module|]";
            var expected = @"Module Module1
    Sub Main()
        For foo to 
    End Sub

    Sub Foo()
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545483, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void DontAddParentheseForMissingName()
        {
            var code = @"[|Class C
    Public Overrides Function|]";
            var expected = @"Class C
    Public Overrides Function";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(545483, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void CombinedDelegates()
        {
            var code = @"[|Imports System
Class A
    Public Shared Operator +(x As A, y As A) As Action
    End Operator
    Shared Sub Main()
        Dim x As New A
        Call x + x
    End Sub
End Class|]";
            var expected = @"Imports System
Class A
    Public Shared Operator +(x As A, y As A) As Action
    End Operator
    Shared Sub Main()
        Dim x As New A
        Call x + x
    End Sub
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(546581, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ThenOmittedWithSurroundingErrors()
        {
            var code = @"[|
        If True OrElse|]";
            var expected = @"
        If True OrElse";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [WorkItem(546581, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ThenOmittedWithSurroundingErrors1()
        {
            var code = @"[|
        If True|]";
            var expected = @"
        If True Then";

            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [WorkItem(546797, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ParenthesisWithLineContinuation()
        {
            var code = @"[|
            System.Diagnostics.Debug.Assert _ (True)|]";
            var expected = @"
        System.Diagnostics.Debug.Assert _ (True)";
            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [WorkItem(546806, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ThenWithLineContinuation()
        {
            var code = @"[|
#If Condition _ Then
            ' blah
#End If|]";
            var expected = @"
#If Condition _ Then
        ' blah
#End If";
            Verify(CreateMethod(code), CreateMethod(expected));
        }

        [Fact]
        [WorkItem(531278, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void ThenInIfDirective()
        {
            var code = @"#Const ccConst = 0
[|#If ccConst
#End If|]
Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
    End Sub
End Module";
            var expected = @"#Const ccConst = 0
#If ccConst Then
#End If
Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
    End Sub
End Module";
            Verify(code, expected);
        }

        [Fact]
        [WorkItem(607792, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void CaseKeywordInSelectStatement()
        {
            var code = @"
Module Program
    Sub Main()
[|
        Select 1
        End Select

        Dim z = Function() From x In """"
        :Select 1
        End Select

        Dim z2 = Function() From x In """" : Select        1
        End Select
|]
    End Sub
End Module";

            var expected = @"
Module Program
    Sub Main()

        Select Case 1
        End Select

        Dim z = Function() From x In """"
        : Select Case 1
        End Select

        Dim z2 = Function() From x In """" : Select Case 1
        End Select

    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(530789, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void Bug530789()
        {
            var code = @"Imports System
Module Program
    Sub Main()
        [|If True Then Console.WriteLine else If False Then Console.WriteLine else Console.writeline|]
    End Sub
End Module";

            var expected = @"Imports System
Module Program
    Sub Main()
        If True Then Console.WriteLine() else If False Then Console.WriteLine() else Console.writeline()
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(530039, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestArraySyntax()
        {
            var code = @"[|Module TestMod
Sub Main()
Dim y As Object
Dim x As cls2(Of y.gettype())
End Sub
End Module|]";

            var expected = @"Module TestMod
    Sub Main()
        Dim y As Object
        Dim x As cls2(Of y.gettype())
    End Sub
End Module";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithoutAsClause()
        {
            var code = @"[|
Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class Test
    Implements I

    '   a. Basic
    Async Function Foo1()
    End Function

    '   b. With Trailing trivia
    Async Function Foo2()   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo4
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo5() Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo6()      Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo7()
    End Sub

    '   g. Without End Function
    Async Function Foo3()

    '   h. With End Function On SameLine
    Async Function Foo4(ByVal x As Integer) End Function

End Class|]";

            var expected = @"
Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class Test
    Implements I

    '   a. Basic
    Async Function Foo1() As System.Threading.Tasks.Task
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As System.Threading.Tasks.Task   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo4() As System.Threading.Tasks.Task
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo5() As System.Threading.Tasks.Task Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo6() As System.Threading.Tasks.Task Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo7()
    End Sub

    '   g. Without End Function
    Async Function Foo3() As System.Threading.Tasks.Task

    '   h. With End Function On SameLine
    Async Function Foo4(ByVal x As Integer) As System.Threading.Tasks.Task End Function

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithoutAsClause_WithAddedImports()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks

Interface I
    Function Foo() As Task
End Interface

Class Test
    Implements I

    '   a. Basic
    Async Function Foo1()
    End Function

    '   b. With Trailing trivia
    Async Function Foo2()   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo4
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo5() Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo6()      Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo7()
    End Sub

    '   g. Without End Function
    Async Function Foo3()

    '   h. With End Function On SameLine
    Async Function Foo4(ByVal x As Integer) End Function

End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks

Interface I
    Function Foo() As Task
End Interface

Class Test
    Implements I

    '   a. Basic
    Async Function Foo1() As Task
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As Task   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo4() As Task
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo5() As Task Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo6() As Task Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo7()
    End Sub

    '   g. Without End Function
    Async Function Foo3() As Task

    '   h. With End Function On SameLine
    Async Function Foo4(ByVal x As Integer) As Task End Function

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithoutAsClause()
        {
            var code = @"[|
Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class Test
    Implements I

    '   a. Basic
    Iterator Function Foo1()
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2()   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo4
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo5() Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo6()      Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo7()
    End Sub

    '   g. Without End Function
    Iterator Function Foo3()

End Class|]";

            var expected = @"
Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class Test
    Implements I

    '   a. Basic
    Iterator Function Foo1() As System.Collections.IEnumerable
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As System.Collections.IEnumerable   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo4() As System.Collections.IEnumerable
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo5() As System.Collections.IEnumerable Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo6() As System.Collections.IEnumerable Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo7()
    End Sub

    '   g. Without End Function
    Iterator Function Foo3() As System.Collections.IEnumerable

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithoutAsClause_WithAddedImports()
        {
            var code = @"[|
Imports System
Imports System.Collections
Imports System.Collections.Generic

Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class Test
    Implements I

    '   a. Basic
    Iterator Function Foo1()
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2()   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo4
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo5() Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo6()      Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo7()
    End Sub

    '   g. Without End Function
    Iterator Function Foo3()

End Class|]";

            var expected = @"
Imports System
Imports System.Collections
Imports System.Collections.Generic

Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class Test
    Implements I

    '   a. Basic
    Iterator Function Foo1() As IEnumerable
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As IEnumerable   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo4() As IEnumerable
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo5() As IEnumerable Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo6() As IEnumerable Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo7()
    End Sub

    '   g. Without End Function
    Iterator Function Foo3() As IEnumerable

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithAsClause()
        {
            var code = @"[|
Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class MyType
    Public Class Task
    End Class
End Class

Class MyTask
    Inherits Task
    Sub New()
        MyBase.New(Nothing)
    End Sub
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Async Function Foo1() As Integer
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As Integer   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo3 As Integer   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo4() As Integer Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo5() As Integer     Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo6() As Integer
    End Sub

    '   g. With valid Task return type
    Async Function Foo7() As Task
    End Function

    '   h. With valid Task(Of T) return type
    Async Function Foo8() As Task(Of T)
    End Function

    '   i. With valid Task(Of Integer) return type
    Async Function Foo9() As Task(Of Integer)
    End Function

    '   j. With invalid user defined Task return type
    Async Function Foo10() As MyType.Task
    End Function

    '   k. With error return type
    Async Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Async Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Async Function Foo13()    As         Integer  ' Trailing
    End Function

    '   n. With return type that inherits from Task
    Async Function Foo14() As MyTask
    End Function

    '   o. Without End Function
    Async Function FooLast() As Integer

End Class|]";

            var expected = @"
Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class MyType
    Public Class Task
    End Class
End Class

Class MyTask
    Inherits Task
    Sub New()
        MyBase.New(Nothing)
    End Sub
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Async Function Foo1() As System.Threading.Tasks.Task(Of Integer)
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As System.Threading.Tasks.Task(Of Integer)   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo3() As System.Threading.Tasks.Task(Of Integer)   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo4() As System.Threading.Tasks.Task(Of Integer) Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo5() As System.Threading.Tasks.Task(Of Integer) Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo6() As Integer
    End Sub

    '   g. With valid Task return type
    Async Function Foo7() As Task
    End Function

    '   h. With valid Task(Of T) return type
    Async Function Foo8() As Task(Of T)
    End Function

    '   i. With valid Task(Of Integer) return type
    Async Function Foo9() As Task(Of Integer)
    End Function

    '   j. With invalid user defined Task return type
    Async Function Foo10() As System.Threading.Tasks.Task(Of MyType.Task)
    End Function

    '   k. With error return type
    Async Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Async Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Async Function Foo13() As System.Threading.Tasks.Task(Of Integer)  ' Trailing
    End Function

    '   n. With return type that inherits from Task
    Async Function Foo14() As System.Threading.Tasks.Task(Of MyTask)
    End Function

    '   o. Without End Function
    Async Function FooLast() As System.Threading.Tasks.Task(Of Integer)

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithAsClause_WithAddedImports()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks

Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class MyType
    Public Class Task
    End Class
End Class

Class MyTask
    Inherits Task
    Sub New()
        MyBase.New(Nothing)
    End Sub
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Async Function Foo1() As Integer
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As Integer   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo3 As Integer   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo4() As Integer Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo5() As Integer     Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo6() As Integer
    End Sub

    '   g. With valid Task return type
    Async Function Foo7() As Task
    End Function

    '   h. With valid Task(Of T) return type
    Async Function Foo8() As Task(Of T)
    End Function

    '   i. With valid Task(Of Integer) return type
    Async Function Foo9() As Task(Of Integer)
    End Function

    '   j. With invalid user defined Task return type
    Async Function Foo10() As MyType.Task
    End Function

    '   k. With error return type
    Async Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Async Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Async Function Foo13()    As         Integer  ' Trailing
    End Function

    '   n. With return type that inherits from Task
    Async Function Foo14() As MyTask
    End Function

    '   o. Without End Function
    Async Function FooLast() As Integer

End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks

Interface I
    Function Foo() As System.Threading.Tasks.Task
End Interface

Class MyType
    Public Class Task
    End Class
End Class

Class MyTask
    Inherits Task
    Sub New()
        MyBase.New(Nothing)
    End Sub
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Async Function Foo1() As Task(Of Integer)
    End Function

    '   b. With Trailing trivia
    Async Function Foo2() As Task(Of Integer)   ' Trailing
    End Function

    '   c. Without Parenthesis
    Async Function Foo3() As Task(Of Integer)   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Async Function Foo4() As Task(Of Integer) Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Async Function Foo5() As Task(Of Integer) Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Async Sub
    Async Sub Foo6() As Integer
    End Sub

    '   g. With valid Task return type
    Async Function Foo7() As Task
    End Function

    '   h. With valid Task(Of T) return type
    Async Function Foo8() As Task(Of T)
    End Function

    '   i. With valid Task(Of Integer) return type
    Async Function Foo9() As Task(Of Integer)
    End Function

    '   j. With invalid user defined Task return type
    Async Function Foo10() As Task(Of MyType.Task)
    End Function

    '   k. With error return type
    Async Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Async Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Async Function Foo13() As Task(Of Integer)  ' Trailing
    End Function

    '   n. With return type that inherits from Task
    Async Function Foo14() As Task(Of MyTask)
    End Function

    '   o. Without End Function
    Async Function FooLast() As Task(Of Integer)

End Class";

            Verify(code, expected);
        }
        
        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithAsClause()
        {
            var code = @"[|
Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class MyType
    Public Class IEnumerable
    End Class
End Class

Class MyIEnumerable
    Implements System.Collections.IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Iterator Function Foo1() As Integer
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As Integer   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo3 As Integer   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo4() As Integer Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo5() As Integer     Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo6() As Integer
    End Sub

    '   g1. With valid IEnumerable return type
    Iterator Function Foo7_1() As System.Collections.IEnumerable
    End Function

    '   g2. With valid IEnumerator return type
    Iterator Function Foo7_2() As System.Collections.IEnumerator
    End Function

    '   h1. With valid IEnumerable(Of T) return type
    Iterator Function Foo8_1() As System.Collections.Generic.IEnumerable(Of T)
    End Function

    '   h2. With valid IEnumerator(Of T) return type
    Iterator Function Foo8_2() As System.Collections.Generic.IEnumerator(Of T)
    End Function

    '   i1. With valid IEnumerable(Of Integer) return type
    Iterator Function Foo9_1() As System.Collections.Generic.IEnumerable(Of Integer)
    End Function

    '   i2. With valid IEnumerator(Of Integer) return type
    Iterator Function Foo9_2() As System.Collections.Generic.IEnumerator(Of Integer)
    End Function

    '   j. With invalid user defined IEnumerable return type
    Iterator Function Foo10() As MyType.IEnumerable
    End Function

    '   k. With error return type
    Iterator Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Iterator Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Iterator Function Foo13()    As         Integer  ' Trailing
    End Function

    '   n. With return type that implements IEnumerable
    Iterator Function Foo14() As MyIEnumerable
    End Function

    '   o. Without End Function
    Iterator Function FooLast() As Integer

End Class|]";

            var expected = @"
Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class MyType
    Public Class IEnumerable
    End Class
End Class

Class MyIEnumerable
    Implements System.Collections.IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Iterator Function Foo1() As System.Collections.Generic.IEnumerable(Of Integer)
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As System.Collections.Generic.IEnumerable(Of Integer)   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo3() As System.Collections.Generic.IEnumerable(Of Integer)   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo4() As System.Collections.Generic.IEnumerable(Of Integer) Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo5() As System.Collections.Generic.IEnumerable(Of Integer) Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo6() As Integer
    End Sub

    '   g1. With valid IEnumerable return type
    Iterator Function Foo7_1() As System.Collections.IEnumerable
    End Function

    '   g2. With valid IEnumerator return type
    Iterator Function Foo7_2() As System.Collections.IEnumerator
    End Function

    '   h1. With valid IEnumerable(Of T) return type
    Iterator Function Foo8_1() As System.Collections.Generic.IEnumerable(Of T)
    End Function

    '   h2. With valid IEnumerator(Of T) return type
    Iterator Function Foo8_2() As System.Collections.Generic.IEnumerator(Of T)
    End Function

    '   i1. With valid IEnumerable(Of Integer) return type
    Iterator Function Foo9_1() As System.Collections.Generic.IEnumerable(Of Integer)
    End Function

    '   i2. With valid IEnumerator(Of Integer) return type
    Iterator Function Foo9_2() As System.Collections.Generic.IEnumerator(Of Integer)
    End Function

    '   j. With invalid user defined IEnumerable return type
    Iterator Function Foo10() As System.Collections.Generic.IEnumerable(Of MyType.IEnumerable)
    End Function

    '   k. With error return type
    Iterator Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Iterator Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Iterator Function Foo13() As System.Collections.Generic.IEnumerable(Of Integer)  ' Trailing
    End Function

    '   n. With return type that implements IEnumerable
    Iterator Function Foo14() As System.Collections.Generic.IEnumerable(Of MyIEnumerable)
    End Function

    '   o. Without End Function
    Iterator Function FooLast() As System.Collections.Generic.IEnumerable(Of Integer)

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithAsClause_WithAddedImports()
        {
            var code = @"[|
Imports System
Imports System.Collections.Generic

Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class MyType
    Public Class IEnumerable
    End Class
End Class

Class MyIEnumerable
    Implements System.Collections.IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Iterator Function Foo1() As Integer
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As Integer   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo3 As Integer   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo4() As Integer Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo5() As Integer     Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo6() As Integer
    End Sub

    '   g1. With valid IEnumerable return type
    Iterator Function Foo7_1() As System.Collections.IEnumerable
    End Function

    '   g2. With valid IEnumerator return type
    Iterator Function Foo7_2() As System.Collections.IEnumerator
    End Function

    '   h1. With valid IEnumerable(Of T) return type
    Iterator Function Foo8_1() As IEnumerable(Of T)
    End Function

    '   h2. With valid IEnumerator(Of T) return type
    Iterator Function Foo8_2() As System.Collections.Generic.IEnumerator(Of T)
    End Function

    '   i1. With valid IEnumerable(Of Integer) return type
    Iterator Function Foo9_1() As IEnumerable(Of Integer)
    End Function

    '   i2. With valid IEnumerator(Of Integer) return type
    Iterator Function Foo9_2() As System.Collections.Generic.IEnumerator(Of Integer)
    End Function

    '   j. With invalid user defined IEnumerable return type
    Iterator Function Foo10() As MyType.IEnumerable
    End Function

    '   k. With error return type
    Iterator Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Iterator Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Iterator Function Foo13()    As         Integer  ' Trailing
    End Function

    '   n. With return type that implements IEnumerable
    Iterator Function Foo14() As MyIEnumerable
    End Function

    '   o. Without End Function
    Iterator Function FooLast() As Integer

End Class|]";

            var expected = @"
Imports System
Imports System.Collections.Generic

Interface I
    Function Foo() As System.Collections.IEnumerable
End Interface

Class MyType
    Public Class IEnumerable
    End Class
End Class

Class MyIEnumerable
    Implements System.Collections.IEnumerable

    Public Function GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Throw New NotImplementedException()
    End Function
End Class

Class Test(Of T)
    Implements I

    '   a. Basic
    Iterator Function Foo1() As IEnumerable(Of Integer)
    End Function

    '   b. With Trailing trivia
    Iterator Function Foo2() As IEnumerable(Of Integer)   ' Trailing
    End Function

    '   c. Without Parenthesis
    Iterator Function Foo3() As IEnumerable(Of Integer)   ' Trailing
    End Function

    '   d. With Implements/Handles clause
    Iterator Function Foo4() As IEnumerable(Of Integer) Implements I.Foo
    End Function

    '   e. With Implements/Handles clause with trivia
    Iterator Function Foo5() As IEnumerable(Of Integer) Implements I.Foo  ' Trailing
    End Function

    '   f. Invalid Iterator Sub
    Iterator Sub Foo6() As Integer
    End Sub

    '   g1. With valid IEnumerable return type
    Iterator Function Foo7_1() As System.Collections.IEnumerable
    End Function

    '   g2. With valid IEnumerator return type
    Iterator Function Foo7_2() As System.Collections.IEnumerator
    End Function

    '   h1. With valid IEnumerable(Of T) return type
    Iterator Function Foo8_1() As IEnumerable(Of T)
    End Function

    '   h2. With valid IEnumerator(Of T) return type
    Iterator Function Foo8_2() As System.Collections.Generic.IEnumerator(Of T)
    End Function

    '   i1. With valid IEnumerable(Of Integer) return type
    Iterator Function Foo9_1() As IEnumerable(Of Integer)
    End Function

    '   i2. With valid IEnumerator(Of Integer) return type
    Iterator Function Foo9_2() As System.Collections.Generic.IEnumerator(Of Integer)
    End Function

    '   j. With invalid user defined IEnumerable return type
    Iterator Function Foo10() As IEnumerable(Of MyType.IEnumerable)
    End Function

    '   k. With error return type
    Iterator Function Foo11() As ErrorType
    End Function

    '   l. Without a return type
    Iterator Function Foo12() As  ' Trailing
    End Function

    '   m. With trivia within AsClause
    Iterator Function Foo13() As IEnumerable(Of Integer)  ' Trailing
    End Function

    '   n. With return type that implements IEnumerable
    Iterator Function Foo14() As IEnumerable(Of MyIEnumerable)
    End Function

    '   o. Without End Function
    Iterator Function FooLast() As IEnumerable(Of Integer)

End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithAliasedReturnType()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks
Imports X = System.Threading.Tasks.Task
Imports Y = System.Threading
Class Test
    Async Function Foo() As X
    End Function
    Async Function Bar() As Y.Tasks.Task
End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks
Imports X = System.Threading.Tasks.Task
Imports Y = System.Threading
Class Test
    Async Function Foo() As X
    End Function
    Async Function Bar() As Y.Tasks.Task
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithAliasedReturnType()
        {
            var code = @"[|
Imports System
Imports System.Collections
Imports X = System.Collections.IEnumerable
Imports Y = System.Collections
Class Test
    Iterator Function Foo() As X
    End Function
    Iterator Function Bar() As Y.IEnumerable
End Class|]";

            var expected = @"
Imports System
Imports System.Collections
Imports X = System.Collections.IEnumerable
Imports Y = System.Collections
Class Test
    Iterator Function Foo() As X
    End Function
    Iterator Function Bar() As Y.IEnumerable
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithAliasedReturnType_2()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks
Imports Y = System.Int32

Class Test
    Async Function Foo() As Y      ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks
Imports Y = System.Int32

Class Test
    Async Function Foo() As Task(Of Y)      ' Trailing
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithAliasedReturnType_2()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks
Imports Y = System.Int32

Class Test
    Iterator Function Foo() As Y      ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks
Imports Y = System.Int32

Class Test
    Iterator Function Foo() As Collections.Generic.IEnumerable(Of Y)      ' Trailing
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncFunctionWithQualifiedNameReturnType()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks

Class Test
    Async Function Foo() As System.Int32      ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks

Class Test
    Async Function Foo() As Task(Of System.Int32)      ' Trailing
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorFunctionWithQualifiedNameReturnType()
        {
            var code = @"[|
Imports System
Imports System.Collections

Class Test
    Iterator Function Foo() As System.Int32      ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Collections

Class Test
    Iterator Function Foo() As Generic.IEnumerable(Of System.Int32)      ' Trailing
    End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestAsyncLambdaFunction()
        {
            var code = @"[|
Imports System
Imports System.Threading.Tasks

Class Test
    Async Function Foo() As Integer
        ' Basic
        Dim a = Async Function() As Integer
                End Function

        ' With trailing trivia
        Dim b = Async Function() As Integer    ' Trailing
                End Function

        ' Single Line lambda
        Dim c = Async Function() 0

        ' Without AsClause
        Dim d = Async Function()
                End Function

        ' With valid Task return type
        Dim e = Async Function() as Task
                End Function

        ' Without End Function
        Dim last = Async Function() As Integer    ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Threading.Tasks

Class Test
    Async Function Foo() As Task(Of Integer)
        ' Basic
        Dim a = Async Function() As Task(Of Integer)
                End Function

        ' With trailing trivia
        Dim b = Async Function() As Task(Of Integer)    ' Trailing
                End Function

        ' Single Line lambda
        Dim c = Async Function() 0

        ' Without AsClause
        Dim d = Async Function()
                End Function

        ' With valid Task return type
        Dim e = Async Function() as Task
                End Function

        ' Without End Function
        Dim last = Async Function() As Task(Of Integer)    ' Trailing
        End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [WorkItem(602932, "DevDiv")]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestIteratorLambdaFunction()
        {
            var code = @"[|
Imports System
Imports System.Collections.Generic

Class Test
    Iterator Function Foo() As Integer
        ' Basic
        Dim a = Iterator Function() As Integer
                End Function

        ' With trailing trivia
        Dim b = Iterator Function() As Integer    ' Trailing
                End Function

        ' Single Line lambda
        Dim c = Iterator Function() 0

        ' Without AsClause
        Dim d = Iterator Function()
                End Function

        ' With valid IEnumerable return type
        Dim e = Iterator Function() as System.Collections.IEnumerable
                End Function

        ' Without End Function
        Dim last = Iterator Function() As Integer    ' Trailing
    End Function
End Class|]";

            var expected = @"
Imports System
Imports System.Collections.Generic

Class Test
    Iterator Function Foo() As IEnumerable(Of Integer)
        ' Basic
        Dim a = Iterator Function() As IEnumerable(Of Integer)
                End Function

        ' With trailing trivia
        Dim b = Iterator Function() As IEnumerable(Of Integer)    ' Trailing
                End Function

        ' Single Line lambda
        Dim c = Iterator Function() 0

        ' Without AsClause
        Dim d = Iterator Function()
                End Function

        ' With valid IEnumerable return type
        Dim e = Iterator Function() as System.Collections.IEnumerable
                End Function

        ' Without End Function
        Dim last = Iterator Function() As IEnumerable(Of Integer)    ' Trailing
        End Function
End Class";

            Verify(code, expected);
        }

        [Fact]
        [Trait(Traits.Feature, Traits.Features.AddMissingTokens)]
        public void TestNoParentheseForArgument()
        {
            // making roslyn behavior same as dev12
            // also, this is one of most expensive one to check whether
            // parenthese needs to be inserted or not.

            var code = @"[|
Imports System
Imports System.Collections.Generic

Class Test
    Private Function Foo() As Integer
        Return 1
    End Function

    Private Sub Caller(i As Integer)
        Caller(Foo)
    End Sub
End Class|]";

            var expected = @"
Imports System
Imports System.Collections.Generic

Class Test
    Private Function Foo() As Integer
        Return 1
    End Function

    Private Sub Caller(i As Integer)
        Caller(Foo)
    End Sub
End Class";

            Verify(code, expected);
        }

        private string CreateMethod(string body)
        {
            return @"Imports System
Class C
    Public Sub Method()" + body + @"
    End Sub
End Class";
        }

        private void Verify(string codeWithMarker, string expectedResult)
        {
            var codeWithoutMarker = default(string);
            var textSpans = (IList<TextSpan>)new List<TextSpan>();
            MarkupTestFile.GetSpans(codeWithMarker, out codeWithoutMarker, out textSpans);

            var document = CreateDocument(codeWithoutMarker, LanguageNames.VisualBasic);
            var codeCleanups = CodeCleaner.GetDefaultProviders(document).Where(p => p.Name == PredefinedCodeCleanupProviderNames.AddMissingTokens || p.Name == PredefinedCodeCleanupProviderNames.Format || p.Name == PredefinedCodeCleanupProviderNames.Simplification);

            var cleanDocument = CodeCleaner.CleanupAsync(document, textSpans[0], codeCleanups).Result;

            Assert.Equal(expectedResult, cleanDocument.GetSyntaxRootAsync().Result.ToFullString());
        }

        private static Document CreateDocument(string code, string language)
        {
            var solution = new CustomWorkspace().CurrentSolution;
            var projectId = ProjectId.CreateNewId();
            var project = solution.AddProject(projectId, "Project", "Project.dll", language).GetProject(projectId);

            return project.AddMetadataReference(new MetadataFileReference(typeof(int).Assembly.Location))
                          .AddDocument("Document", SourceText.From(code));
        }
    }
}
