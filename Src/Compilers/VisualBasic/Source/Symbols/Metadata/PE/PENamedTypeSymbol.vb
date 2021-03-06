﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Reflection
Imports System.Reflection.Metadata
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind
Imports TypeAttributes = System.Reflection.TypeAttributes
Imports FieldAttributes = System.Reflection.FieldAttributes

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE

    ''' <summary>
    ''' The class to represent all types imported from a PE/module.
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class PENamedTypeSymbol
        Inherits InstanceTypeSymbol

        Private ReadOnly m_Container As NamespaceOrTypeSymbol

#Region "Metadata"
        Private ReadOnly m_Handle As TypeHandle
        Private ReadOnly m_GenericParameterHandles As GenericParameterHandleCollection
        Private ReadOnly m_Name As String
        Private ReadOnly m_Flags As TypeAttributes
        Private ReadOnly m_Arity As UShort
        Private ReadOnly m_MangleName As Boolean ' CONSIDER: combine with flags
#End Region

        ''' <summary>
        ''' A map of types immediately contained within this type 
        ''' grouped by their name (case-insensitively).
        ''' </summary>
        Private m_lazyNestedTypes As Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))

        ''' <summary>
        ''' A set of all the names of the members in this type.
        ''' </summary>
        Private m_lazyMemberNames As ICollection(Of String)

        ''' <summary>
        ''' A map of members immediately contained within this type 
        ''' grouped by their name (case-insensitively).
        ''' </summary>
        ''' <remarks></remarks>
        Private m_lazyMembers As Dictionary(Of String, ImmutableArray(Of Symbol))

        Private m_lazyTypeParameters As ImmutableArray(Of TypeParameterSymbol)

        Private m_lazyEnumUnderlyingType As NamedTypeSymbol

        Private m_lazyCustomAttributes As ImmutableArray(Of VisualBasicAttributeData)
        Private m_lazyConditionalAttributeSymbols As ImmutableArray(Of String)
        Private m_lazyAttributeUsageInfo As AttributeUsageInfo = AttributeUsageInfo.Null

        Private m_lazyCoClassType As TypeSymbol = ErrorTypeSymbol.UnknownResultType

        ''' <summary>
        ''' Lazily initialized by TypeKind property.
        ''' Using Integer type to make sure read/write operations are atomic.
        ''' </summary>
        ''' <remarks></remarks>
        Private m_lazyTypeKind As Integer

        Private m_lazyDocComment As Tuple(Of CultureInfo, String)

        Private m_lazyDefaultPropertyName As String

        Private m_lazyUseSiteErrorInfo As DiagnosticInfo = ErrorFactory.EmptyErrorInfo ' Indicates unknown state. 

        Private m_LazyMightContainExtensionMethods As Byte = ThreeState.Unknown

        Private m_LazyHasEmbeddedAttribute As Integer = ThreeState.Unknown

        Private m_lazyObsoleteAttributeData As ObsoleteAttributeData = ObsoleteAttributeData.Uninitialized

        Private m_lazyIsExtensibleInterface As ThreeState = ThreeState.Unknown

        Friend Sub New(
            moduleSymbol As PEModuleSymbol,
            containingNamespace As PENamespaceSymbol,
            handle As TypeHandle
        )
            Me.New(moduleSymbol, containingNamespace, 0, handle)
        End Sub

        Friend Sub New(
            moduleSymbol As PEModuleSymbol,
            containingType As PENamedTypeSymbol,
            handle As TypeHandle
        )
            Me.New(moduleSymbol, containingType, CUShort(containingType.MetadataArity), handle)
        End Sub

        Private Sub New(
            moduleSymbol As PEModuleSymbol,
            container As NamespaceOrTypeSymbol,
            containerMetadataArity As UShort,
            handle As TypeHandle
        )
            Debug.Assert(Not handle.IsNil)
            Debug.Assert(container IsNot Nothing)

            m_Handle = handle
            m_Container = container

            Dim makeBad As Boolean = False

            Dim name As String

            Try
                name = moduleSymbol.Module.GetTypeDefNameOrThrow(handle)
            Catch mrEx As BadImageFormatException
                name = String.Empty
                makeBad = True
            End Try

            Try
                m_Flags = moduleSymbol.Module.GetTypeDefFlagsOrThrow(handle)
            Catch mrEx As BadImageFormatException
                makeBad = True
            End Try

            Dim metadataArity As Integer

            Try
                m_GenericParameterHandles = moduleSymbol.Module.GetTypeDefGenericParamsOrThrow(handle)
                metadataArity = m_GenericParameterHandles.Count
            Catch mrEx As BadImageFormatException
                m_GenericParameterHandles = Nothing
                metadataArity = 0
                makeBad = True
            End Try

            ' Figure out arity from the language point of view
            If metadataArity > containerMetadataArity Then
                m_Arity = CType(metadataArity - containerMetadataArity, UShort)
            End If

            If m_Arity = 0 Then
                m_lazyTypeParameters = ImmutableArray(Of TypeParameterSymbol).Empty
                m_Name = name
                m_MangleName = False
            Else
                ' Unmangle name for a generic type.
                m_Name = MetadataHelpers.UnmangleMetadataNameForArity(name, m_Arity)
                m_MangleName = (m_Name IsNot name)
            End If

            If makeBad OrElse metadataArity < containerMetadataArity Then
                m_lazyUseSiteErrorInfo = ErrorFactory.ErrorInfo(ERRID.ERR_UnsupportedType1, Me)
            End If

            Debug.Assert(Not m_MangleName OrElse m_Name.Length < name.Length)
        End Sub

        Friend ReadOnly Property ContainingPEModule As PEModuleSymbol
            Get
                Dim s As Symbol = m_Container

                While s.Kind <> SymbolKind.Namespace
                    s = s.ContainingSymbol
                End While

                Return DirectCast(s, PENamespaceSymbol).ContainingPEModule
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingModule As ModuleSymbol
            Get
                Return ContainingPEModule
            End Get
        End Property

        Public Overrides ReadOnly Property Arity As Integer
            Get
                Return m_Arity
            End Get
        End Property

        Friend Overrides ReadOnly Property MangleName As Boolean
            Get
                Return m_MangleName
            End Get
        End Property

        Friend Overrides ReadOnly Property Layout As TypeLayout
            Get
                Return Me.ContainingPEModule.Module.GetTypeLayout(m_Handle)
            End Get
        End Property

        Friend Overrides ReadOnly Property MarshallingCharSet As CharSet
            Get
                Dim result As CharSet = m_Flags.ToCharSet()
                If result = 0 Then
                    Return CharSet.Ansi
                End If

                Return result
            End Get
        End Property

        Friend Overrides ReadOnly Property IsSerializable As Boolean
            Get
                Return (m_Flags And TypeAttributes.Serializable) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property HasSpecialName As Boolean
            Get
                Return (m_Flags And TypeAttributes.SpecialName) <> 0
            End Get
        End Property

        Friend ReadOnly Property MetadataArity As Integer
            Get
                Return m_GenericParameterHandles.Count
            End Get
        End Property

        Friend ReadOnly Property Handle As TypeHandle
            Get
                Return m_Handle
            End Get
        End Property

        Friend Overrides Function GetInterfacesToEmit() As IEnumerable(Of NamedTypeSymbol)
            Return InterfacesNoUseSiteDiagnostics
        End Function

        Friend Overrides Function MakeDeclaredBase(basesBeingResolved As ConsList(Of Symbol), diagnostics As DiagnosticBag) As NamedTypeSymbol
            If (Me.m_Flags And TypeAttributes.Interface) = 0 Then
                Dim moduleSymbol As PEModuleSymbol = Me.ContainingPEModule

                Try
                    Dim token As Handle = moduleSymbol.Module.GetBaseTypeOfTypeOrThrow(Me.m_Handle)
                    If Not token.IsNil Then
                        Return DirectCast(New MetadataDecoder(moduleSymbol, Me).GetTypeOfToken(token), NamedTypeSymbol)
                    End If
                Catch mrEx As BadImageFormatException
                    Return New UnsupportedMetadataTypeSymbol(mrEx)
                End Try
            End If
            Return Nothing
        End Function

        Friend Overrides Function MakeDeclaredInterfaces(basesBeingResolved As ConsList(Of Symbol), diagnostics As DiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Try
                Dim moduleSymbol As PEModuleSymbol = Me.ContainingPEModule
                Dim interfaceImpls = moduleSymbol.Module.GetImplementedInterfacesOrThrow(Me.m_Handle)

                If interfaceImpls.Count = 0 Then
                    Return ImmutableArray(Of NamedTypeSymbol).Empty
                End If

                Dim symbols As NamedTypeSymbol() = New NamedTypeSymbol(interfaceImpls.Count - 1) {}
                Dim tokenDecoder As New MetadataDecoder(moduleSymbol, Me)
                Dim i = 0
                For Each interfaceImpl In interfaceImpls

                    Dim namedTypeSymbol As NamedTypeSymbol = TryCast(tokenDecoder.GetTypeOfToken(interfaceImpl), NamedTypeSymbol)
                    'TODO: how to pass reason to unsupported
                    symbols(i) = If(namedTypeSymbol IsNot Nothing, namedTypeSymbol, New UnsupportedMetadataTypeSymbol()) ' "interface tmpList contains a bad type"
                    i = i + 1
                Next

                Return symbols.AsImmutableOrNull

            Catch mrEx As BadImageFormatException
                Return ImmutableArray.Create(Of NamedTypeSymbol)(New UnsupportedMetadataTypeSymbol(mrEx))
            End Try
        End Function

        Private Shared Function CyclicInheritanceError(diag As DiagnosticInfo) As ErrorTypeSymbol
            Return New ExtendedErrorTypeSymbol(diag, True)
        End Function

        Friend Overrides Function MakeAcyclicBaseType(diagnostics As DiagnosticBag) As NamedTypeSymbol
            Dim diag = BaseTypeAnalysis.GetDependencyDiagnosticsForImportedClass(Me)
            If diag IsNot Nothing Then
                Return CyclicInheritanceError(diag)
            End If

            Return GetDeclaredBase(Nothing)
        End Function

        Friend Overrides Function MakeAcyclicInterfaces(diagnostics As DiagnosticBag) As ImmutableArray(Of NamedTypeSymbol)
            Dim declaredInterfaces As ImmutableArray(Of NamedTypeSymbol) = GetDeclaredInterfacesNoUseSiteDiagnostics(Nothing)
            If (Not Me.IsInterface) Then
                ' only interfaces needs to check for inheritance cycles via interfaces.
                Return declaredInterfaces
            End If

            Return (From t In declaredInterfaces
                    Let diag = BaseTypeAnalysis.GetDependencyDiagnosticsForImportedBaseInterface(Me, t)
                    Select If(diag Is Nothing, t, CyclicInheritanceError(diag))).AsImmutable

        End Function

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return m_Container
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return TryCast(m_Container, NamedTypeSymbol)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Dim access As Accessibility = Accessibility.Private

                Select Case m_Flags And TypeAttributes.VisibilityMask
                    Case TypeAttributes.NestedAssembly
                        access = Accessibility.Friend

                    Case TypeAttributes.NestedFamORAssem
                        access = Accessibility.ProtectedOrFriend

                    Case TypeAttributes.NestedFamANDAssem
                        access = Accessibility.ProtectedAndFriend

                    Case TypeAttributes.NestedPrivate
                        access = Accessibility.Private

                    Case TypeAttributes.Public,
                         TypeAttributes.NestedPublic
                        access = Accessibility.Public

                    Case TypeAttributes.NestedFamily
                        access = Accessibility.Protected

                    Case TypeAttributes.NotPublic
                        access = Accessibility.Friend

                    Case Else
                        Debug.Assert(False, "Unexpected!!!")
                End Select

                Return access
            End Get
        End Property

        Public Overrides ReadOnly Property EnumUnderlyingType As NamedTypeSymbol
            Get
                If m_lazyEnumUnderlyingType Is Nothing AndAlso TypeKind = TypeKind.Enum Then
                    ' From §8.5.2
                    ' An enum is considerably more restricted than a true type, as
                    ' follows:
                    ' • It shall have exactly one instance field, and the type of that field defines the underlying type of
                    ' the enumeration.
                    ' • It shall not have any static fields unless they are literal. (see §8.6.1.2)

                    ' The underlying type shall be a built-in integer type. Enums shall derive from System.Enum, hence they are
                    ' value types. Like all value types, they shall be sealed (see §8.9.9).

                    Dim underlyingType As NamedTypeSymbol = Nothing
                    For Each member In GetMembers()
                        If (Not member.IsShared AndAlso member.Kind = SymbolKind.Field) Then
                            Dim type = DirectCast(member, FieldSymbol).Type

                            If (type.SpecialType.IsClrInteger()) Then
                                If (underlyingType Is Nothing) Then
                                    underlyingType = DirectCast(type, NamedTypeSymbol)
                                Else
                                    underlyingType = New UnsupportedMetadataTypeSymbol()
                                    Exit For
                                End If
                            End If
                        End If
                    Next

                    Interlocked.CompareExchange(m_lazyEnumUnderlyingType,
                        If(underlyingType, New UnsupportedMetadataTypeSymbol()),
                        Nothing)
                End If

                Return m_lazyEnumUnderlyingType
            End Get
        End Property

        Public Overloads Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            If m_lazyCustomAttributes.IsDefault Then
                If (m_lazyTypeKind = TypeKind.Unknown AndAlso
                    ((m_Flags And TypeAttributes.Interface) <> 0 OrElse Me.Arity <> 0 OrElse Me.ContainingType IsNot Nothing)) OrElse
                   Me.TypeKind <> TypeKind.Module Then
                    ContainingPEModule.LoadCustomAttributes(m_Handle, m_lazyCustomAttributes)
                Else
                    Dim stdModuleAttribute As CustomAttributeHandle
                    Dim attributes = ContainingPEModule.GetCustomAttributesForToken(
                        m_Handle,
                        stdModuleAttribute,
                        filterOut1:=AttributeDescription.StandardModuleAttribute)

                    Debug.Assert(Not stdModuleAttribute.IsNil)
                    ImmutableInterlocked.InterlockedInitialize(m_lazyCustomAttributes, attributes)
                End If
            End If

            Return m_lazyCustomAttributes
        End Function

        Friend Overrides Iterator Function GetCustomAttributesToEmit() As IEnumerable(Of VisualBasicAttributeData)
            For Each attribute In GetAttributes()
                Yield attribute
            Next

            If Me.TypeKind = TypeKind.Module Then
                Yield New PEAttributeData(ContainingPEModule,
                                          ContainingPEModule.Module.GetAttributeHandle(Me.m_Handle, AttributeDescription.StandardModuleAttribute))
            End If
        End Function

        Public Overrides ReadOnly Property MemberNames As IEnumerable(Of String)
            Get
                EnsureNonTypeMemberNamesAreLoaded()
                Return m_lazyMemberNames
            End Get
        End Property

        Private Sub EnsureNonTypeMemberNamesAreLoaded()
            If m_lazyMemberNames Is Nothing Then

                Dim peModule = ContainingPEModule.Module
                Dim names = New HashSet(Of String)()

                Try
                    For Each methodDef In peModule.GetMethodsOfTypeOrThrow(m_Handle)
                        Try
                            names.Add(peModule.GetMethodDefNameOrThrow(methodDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Try
                    For Each fieldDef In peModule.GetFieldsOfTypeOrThrow(m_Handle)
                        Try
                            names.Add(peModule.GetFieldDefNameOrThrow(fieldDef))
                        Catch mrEx As BadImageFormatException
                        End Try
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                Interlocked.CompareExchange(Of ICollection(Of String))(
                    m_lazyMemberNames,
                    SpecializedCollections.ReadOnlySet(names),
                    Nothing)
            End If
        End Sub

        Public Overloads Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Return m_lazyMembers.Flatten(DeclarationOrderSymbolComparer.Instance)
        End Function

        Friend Overrides Function GetMembersUnordered() As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Dim result = m_lazyMembers.Flatten()

#If DEBUG Then
            ' In DEBUG, swap first and last elements so that use of Unordered in a place it isn't warranted is caught
            ' more obviously.
            Return result.DeOrder()
#Else
            Return result
#End If
        End Function

        Friend Overrides Function GetFieldsToEmit() As IEnumerable(Of FieldSymbol)
            ' If there are any fields, they are at the very beginning.
            Return GetMembers(Of FieldSymbol)(GetMembers(), SymbolKind.Field, offset:=0)
        End Function

        Friend Overrides Iterator Function GetMethodsToEmit() As IEnumerable(Of MethodSymbol)
            Dim members = GetMembers()

            ' Get to methods.
            Dim index = GetIndexOfFirstMember(members, SymbolKind.Method)

            If Not Me.IsInterfaceType() Then
                While index < members.Length
                    Dim member = members(index)
                    If member.Kind <> SymbolKind.Method Then
                        Exit While
                    End If

                    Dim method = DirectCast(member, MethodSymbol)

                    ' Don't emit the default value type constructor - the runtime handles that
                    If Not method.IsParameterlessStructConstructor(requireSynthesized:=True) Then
                        Yield method
                    End If

                    index += 1
                End While

            Else
                ' We do not create symbols for v-table gap methods, let's figure out where the gaps go.
                If index >= members.Length OrElse members(index).Kind <> SymbolKind.Method Then
                    ' We didn't import any methods, it is Ok to return an empty set.
                    Return
                End If

                Dim method = DirectCast(members(index), PEMethodSymbol)
                Dim [module] = ContainingPEModule.Module
                Dim methodDefs = ArrayBuilder(Of MethodHandle).GetInstance()

                Try
                    For Each methodDef In [module].GetMethodsOfTypeOrThrow(m_Handle)
                        methodDefs.Add(methodDef)
                    Next
                Catch mrEx As BadImageFormatException
                End Try

                For Each methodDef In methodDefs
                    If method.Handle = methodDef Then
                        Yield method
                        index += 1

                        If index = members.Length OrElse members(index).Kind <> SymbolKind.Method Then
                            ' no need to return any gaps at the end.
                            methodDefs.Free()
                            Return
                        End If

                        method = DirectCast(members(index), PEMethodSymbol)

                    Else
                        ' Encountered a gap.
                        Dim gapSize As Integer

                        Try
                            gapSize = ModuleExtensions.GetVTableGapSize([module].GetMethodDefNameOrThrow(methodDef))
                        Catch mrEx As BadImageFormatException
                            gapSize = 1
                        End Try

                        ' We don't have a symbol to return, so, even if the name doesn't represent a gap, we still have a gap.
                        Do
                            Yield Nothing
                            gapSize -= 1
                        Loop While gapSize > 0
                    End If
                Next

                ' Ensure we explicitly returned from inside loop.
                Throw ExceptionUtilities.Unreachable
            End If
        End Function

        Friend Overrides Function GetPropertiesToEmit() As IEnumerable(Of PropertySymbol)
            Return GetMembers(Of PropertySymbol)(GetMembers(), SymbolKind.Property)
        End Function

        Friend Overrides Function GetEventsToEmit() As IEnumerable(Of EventSymbol)
            Return GetMembers(Of EventSymbol)(GetMembers(), SymbolKind.Event)
        End Function

        Private Class DeclarationOrderSymbolComparer
            Implements IComparer(Of ISymbol)

            Public Shared ReadOnly Instance As New DeclarationOrderSymbolComparer()

            Private Sub New()
            End Sub

            Public Function Compare(x As ISymbol, y As ISymbol) As Integer Implements IComparer(Of ISymbol).Compare
                If x Is y Then
                    Return 0
                End If

                Dim cmp As Integer = x.Kind.ToSortOrder - y.Kind.ToSortOrder

                If cmp <> 0 Then
                    Return cmp
                End If

                Select Case x.Kind
                    Case SymbolKind.Field
                        Return HandleComparer.Default.Compare(DirectCast(x, PEFieldSymbol).Handle, DirectCast(y, PEFieldSymbol).Handle)
                    Case SymbolKind.Method
                        If DirectCast(x, MethodSymbol).IsParameterlessStructConstructor(requireSynthesized:=True) Then
                            Return -1
                        ElseIf DirectCast(y, MethodSymbol).IsParameterlessStructConstructor(requireSynthesized:=True) Then
                            Return 1
                        End If

                        Return HandleComparer.Default.Compare(DirectCast(x, PEMethodSymbol).Handle, DirectCast(y, PEMethodSymbol).Handle)
                    Case SymbolKind.Property
                        Return HandleComparer.Default.Compare(DirectCast(x, PEPropertySymbol).Handle, DirectCast(y, PEPropertySymbol).Handle)
                    Case SymbolKind.Event
                        Return HandleComparer.Default.Compare(DirectCast(x, PEEventSymbol).Handle, DirectCast(y, PEEventSymbol).Handle)
                    Case SymbolKind.NamedType
                        Return HandleComparer.Default.Compare(DirectCast(x, PENamedTypeSymbol).Handle, DirectCast(y, PENamedTypeSymbol).Handle)
                    Case Else
                        Throw ExceptionUtilities.UnexpectedValue(x.Kind)
                End Select
            End Function
        End Class

        Private Sub EnsureNonTypeMembersAreLoaded()

            If m_lazyMembers Is Nothing Then
                ' A method may be referenced as an accessor by one or more properties. And,
                ' any of those properties may be "bogus" if one of the property accessors
                ' does not match the property signature. If the method is referenced by at
                ' least one non-bogus property, then the method is created as an accessor,
                ' and (for purposes of error reporting if the method is referenced directly) the
                ' associated property is set (arbitrarily) to the first non-bogus property found
                ' in metadata. If the method is not referenced by any non-bogus properties,
                ' then the method is created as a normal method rather than an accessor.
                ' Create a dictionary of method symbols indexed by metadata row id
                ' (to allow efficient lookup when matching property accessors).
                Dim methodHandleToSymbol As Dictionary(Of MethodHandle, PEMethodSymbol) = CreateMethods()
                Dim members = ArrayBuilder(Of Symbol).GetInstance()

                Dim ensureParameterlessConstructor As Boolean = (TypeKind = TypeKind.Structure OrElse TypeKind = TypeKind.Enum) AndAlso Not IsShared

                For Each member In methodHandleToSymbol.Values
                    members.Add(member)

                    If ensureParameterlessConstructor Then
                        ensureParameterlessConstructor = Not member.IsParameterlessConstructor()
                    End If
                Next

                If ensureParameterlessConstructor Then
                    members.Add(New SynthesizedConstructorSymbol(Nothing, Me, Me.IsShared, False, Nothing, Nothing))
                End If

                ' CreateFields will add withEvent names here if there are any.
                ' Otherwise stays Nothing
                Dim withEventNames As HashSet(Of String) = Nothing

                CreateProperties(methodHandleToSymbol, members)
                CreateFields(members, withEventNames)
                CreateEvents(methodHandleToSymbol, members)

                Dim membersDict As New Dictionary(Of String, ImmutableArray(Of Symbol))(CaseInsensitiveComparison.Comparer)
                Dim groupedMembers = members.
                                     GroupBy(Function(m) m.Name, CaseInsensitiveComparison.Comparer)

                For Each g In groupedMembers
                    membersDict.Add(g.Key, g.ToArray().AsImmutableOrNull())
                Next

                members.Free()

                ' tell WithEvents properties that they are WithEvents properties
                If withEventNames IsNot Nothing Then
                    For Each withEventName In withEventNames
                        Dim weMembers As ImmutableArray(Of Symbol) = Nothing
                        If membersDict.TryGetValue(withEventName, weMembers) Then
                            ' there must be only a single match for a given WithEvents name
                            If weMembers.Length <> 1 Then
                                Continue For
                            End If

                            ' it must be a valid property
                            Dim asProperty = TryCast(weMembers(0), PEPropertySymbol)
                            If asProperty IsNot Nothing AndAlso IsValidWithEventsProperty(asProperty) Then
                                asProperty.SetIsWithEvents(True)
                            End If
                        End If
                    Next
                End If

                ' Merge types into members
                For Each typeSymbols In m_lazyNestedTypes.Values
                    Dim name = typeSymbols(0).Name

                    Dim symbols As ImmutableArray(Of Symbol) = Nothing
                    If Not membersDict.TryGetValue(name, symbols) Then
                        membersDict.Add(name, StaticCast(Of Symbol).From(typeSymbols))
                    Else
                        membersDict(name) = symbols.Concat(StaticCast(Of Symbol).From(typeSymbols))
                    End If
                Next

                Dim exchangeResult = Interlocked.CompareExchange(m_lazyMembers, membersDict, Nothing)

                If exchangeResult Is Nothing Then
                    Dim memberNames = SpecializedCollections.ReadOnlyCollection(membersDict.Keys)
                    Interlocked.Exchange(Of ICollection(Of String))(m_lazyMemberNames, memberNames)
                End If
            End If
        End Sub

        ''' <summary>
        ''' Some simple sanity checks if a property can actually be a withevents property
        ''' </summary>
        Private Function IsValidWithEventsProperty(prop As PEPropertySymbol) As Boolean
            ' NOTE: Dev10 does not make any checks. Just has comment that it could be a good idea to do in Whidbey.
            ' We will check, just to make stuff a bit more robust.
            ' It will be extremely rare that this function would fail though.

            If prop.IsReadOnly Or prop.IsWriteOnly Then
                Return False
            End If

            If Not prop.IsOverridable Then
                Return False
            End If

            Return True
        End Function


        Public Overloads Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            EnsureNestedTypesAreLoaded()
            EnsureNonTypeMembersAreLoaded()

            Dim m As ImmutableArray(Of Symbol) = Nothing

            If m_lazyMembers.TryGetValue(name, m) Then
                Return m
            End If

            Return ImmutableArray(Of Symbol).Empty
        End Function

        Friend Overrides Function GetTypeMembersUnordered() As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Return StaticCast(Of NamedTypeSymbol).From(m_lazyNestedTypes.Flatten())
        End Function

        Public Overloads Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Return StaticCast(Of NamedTypeSymbol).From(m_lazyNestedTypes.Flatten(DeclarationOrderSymbolComparer.Instance))
        End Function

        Private Sub EnsureNestedTypesAreLoaded()

            If m_lazyNestedTypes Is Nothing Then

                Dim typesDict = CreateNestedTypes()
                Interlocked.CompareExchange(m_lazyNestedTypes, typesDict, Nothing)

                ' Build cache of TypeDef Tokens
                ' Potentially this can be done in the background.
                If m_lazyNestedTypes Is typesDict Then
                    ContainingPEModule.OnNewTypeDeclarationsLoaded(typesDict)
                End If
            End If

        End Sub

        Public Overloads Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            EnsureNestedTypesAreLoaded()

            Dim t As ImmutableArray(Of PENamedTypeSymbol) = Nothing

            If m_lazyNestedTypes.TryGetValue(name, t) Then
                Return StaticCast(Of NamedTypeSymbol).From(t)
            End If

            Return ImmutableArray(Of NamedTypeSymbol).Empty
        End Function

        Public Overloads Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            Return GetTypeMembers(name).WhereAsArray(Function(type) type.Arity = arity)
        End Function

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return StaticCast(Of Location).From(ContainingPEModule.MetadataLocation)
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return ImmutableArray(Of SyntaxReference).Empty
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return m_Name
            End Get
        End Property

        Friend ReadOnly Property TypeDefFlags As TypeAttributes
            Get
                Return m_Flags
            End Get
        End Property

        Public Overrides ReadOnly Property TypeParameters As ImmutableArray(Of TypeParameterSymbol)
            Get
                EnsureTypeParametersAreLoaded()
                Return m_lazyTypeParameters
            End Get
        End Property

        Private Sub EnsureTypeParametersAreLoaded()

            If m_lazyTypeParameters.IsDefault Then

                Debug.Assert(m_Arity > 0)

                Dim ownedParams(m_Arity - 1) As PETypeParameterSymbol

                Dim moduleSymbol = ContainingPEModule

                ' If this is a nested type generic parameters in metadata include generic parameters of the outer types.
                Dim firstIndex = m_GenericParameterHandles.Count - Arity

                For i = 0 To ownedParams.Length - 1
                    ownedParams(i) = New PETypeParameterSymbol(moduleSymbol, Me, CUShort(i), m_GenericParameterHandles(firstIndex + i))
                Next

                ImmutableInterlocked.InterlockedCompareExchange(m_lazyTypeParameters,
                                            StaticCast(Of TypeParameterSymbol).From(ownedParams.AsImmutableOrNull),
                                            Nothing)
            End If

        End Sub

        Public Overrides ReadOnly Property IsMustInherit As Boolean
            Get
                Return (m_Flags And TypeAttributes.Abstract) <> 0 AndAlso
                    (m_Flags And TypeAttributes.Sealed) = 0
            End Get
        End Property

        Friend Overrides ReadOnly Property IsMetadataAbstract As Boolean
            Get
                Return (m_Flags And TypeAttributes.Abstract) <> 0
            End Get
        End Property

        Public Overrides ReadOnly Property IsNotInheritable As Boolean
            Get
                Return (m_Flags And TypeAttributes.Sealed) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property IsMetadataSealed As Boolean
            Get
                Return (m_Flags And TypeAttributes.Sealed) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property IsWindowsRuntimeImport As Boolean
            Get
                Return (m_Flags And TypeAttributes.WindowsRuntime) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property ShouldAddWinRTMembers As Boolean
            Get
                Return IsWindowsRuntimeImport
            End Get
        End Property

        Friend Overrides Function GetGuidString(ByRef guidString As String) As Boolean
            Return ContainingPEModule.Module.HasGuidAttribute(m_Handle, guidString)
        End Function

        Public NotOverridable Overrides ReadOnly Property MightContainExtensionMethods As Boolean
            Get
                If m_LazyMightContainExtensionMethods = ThreeState.Unknown Then

                    ' Only top level non-generic types with an Extension attribute are
                    ' valid containers of extension methods.
                    Dim result As Boolean = False

                    If m_Container.Kind = SymbolKind.Namespace AndAlso m_Arity = 0 Then
                        Dim containingModuleSymbol = Me.ContainingPEModule

                        If containingModuleSymbol.MightContainExtensionMethods AndAlso
                           containingModuleSymbol.Module.HasExtensionAttribute(Me.m_Handle, ignoreCase:=True) Then
                            result = True
                        End If
                    End If

                    If result Then
                        m_LazyMightContainExtensionMethods = ThreeState.True
                    Else
                        m_LazyMightContainExtensionMethods = ThreeState.False
                    End If
                End If

                Return m_LazyMightContainExtensionMethods = ThreeState.True
            End Get
        End Property

        Friend Overrides ReadOnly Property HasEmbeddedAttribute As Boolean
            Get
                If Me.m_LazyHasEmbeddedAttribute = ThreeState.Unknown Then
                    Interlocked.CompareExchange(Me.m_LazyHasEmbeddedAttribute,
                                                If(Me.ContainingPEModule.Module.HasVisualBasicEmbeddedAttribute(Me.m_Handle),
                                                   ThreeState.True, ThreeState.False),
                                                ThreeState.Unknown)
                End If
                Return Me.m_LazyHasEmbeddedAttribute = ThreeState.True
            End Get
        End Property

        Friend Overrides Sub BuildExtensionMethodsMap(
            map As Dictionary(Of String, ArrayBuilder(Of MethodSymbol)),
            appendThrough As NamespaceSymbol
        )
            If Me.MightContainExtensionMethods Then
                EnsureNestedTypesAreLoaded()
                EnsureNonTypeMembersAreLoaded()

                If Not appendThrough.BuildExtensionMethodsMap(map, m_lazyMembers) Then
                    ' Didn't find any extension methods, record the fact.
                    m_LazyMightContainExtensionMethods = ThreeState.False
                End If
            End If
        End Sub

        Friend Overrides Sub AddExtensionMethodLookupSymbolsInfo(nameSet As LookupSymbolsInfo,
                                                                  options As LookupOptions,
                                                                  originalBinder As Binder,
                                                                  appendThrough As NamedTypeSymbol)
            If Me.MightContainExtensionMethods Then
                EnsureNestedTypesAreLoaded()
                EnsureNonTypeMembersAreLoaded()

                If Not appendThrough.AddExtensionMethodLookupSymbolsInfo(nameSet, options, originalBinder, m_lazyMembers) Then
                    ' Didn't find any extension methods, record the fact.
                    m_LazyMightContainExtensionMethods = ThreeState.False
                End If
            End If
        End Sub

        Public Overrides ReadOnly Property TypeKind As TypeKind
            Get
                If m_lazyTypeKind = TypeKind.Unknown Then

                    Dim result As TypeKind

                    If (m_Flags And TypeAttributes.Interface) <> 0 Then
                        result = TypeKind.Interface
                    Else
                        Dim base As TypeSymbol = GetDeclaredBase(Nothing)

                        result = TypeKind.Class

                        If base IsNot Nothing Then
                            ' Code is cloned from MetaImport::DoImportBaseAndImplements()
                            Dim baseCorTypeId As SpecialType = base.SpecialType

                            If baseCorTypeId = SpecialType.System_Enum Then
                                ' Enum
                                result = TypeKind.Enum
                            ElseIf baseCorTypeId = SpecialType.System_MulticastDelegate OrElse
                                   (baseCorTypeId = SpecialType.System_Delegate AndAlso Me.SpecialType <> SpecialType.System_MulticastDelegate) Then
                                ' Delegate
                                result = TypeKind.Delegate
                            ElseIf (baseCorTypeId = SpecialType.System_ValueType AndAlso
                                     Me.SpecialType <> SpecialType.System_Enum) Then
                                ' Struct
                                result = TypeKind.Structure
                            ElseIf Me.Arity = 0 AndAlso
                                Me.ContainingType Is Nothing AndAlso
                                ContainingPEModule.Module.HasAttribute(Me.m_Handle, AttributeDescription.StandardModuleAttribute) Then
                                result = TypeKind.Module
                            End If
                        End If
                    End If

                    m_lazyTypeKind = result
                End If

                Return CType(m_lazyTypeKind, TypeKind)
            End Get
        End Property

        Friend Overrides ReadOnly Property IsInterface As Boolean
            Get
                Return (m_Flags And TypeAttributes.Interface) <> 0
            End Get
        End Property

        Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
            ' Note: m_lazyDocComment is passed ByRef
            Return PEDocumentationCommentUtils.GetDocumentationComment(
                Me, ContainingPEModule, preferredCulture, cancellationToken, m_lazyDocComment)
        End Function

        Friend Overrides ReadOnly Property IsComImport As Boolean
            Get
                Return (m_Flags And TypeAttributes.Import) <> 0
            End Get
        End Property

        Friend Overrides ReadOnly Property CoClassType As TypeSymbol
            Get
                Debug.Assert(Me.IsInterfaceType())  ' Should never be called on non-interface types

                If m_lazyCoClassType Is ErrorTypeSymbol.UnknownResultType Then
                    Interlocked.CompareExchange(m_lazyCoClassType,
                                                MakeComImportCoClassType(),
                                                DirectCast(ErrorTypeSymbol.UnknownResultType, TypeSymbol))
                End If

                Return m_lazyCoClassType
            End Get
        End Property

        Private Function MakeComImportCoClassType() As TypeSymbol
            Debug.Assert(Me.IsInterfaceType())  ' Should never be called on non-interface types

            Dim coClassTypeName As String = Nothing
            If Not Me.ContainingPEModule.Module.HasCoClassAttribute(Me.m_Handle, coClassTypeName) Then
                Return Nothing
            End If

            Dim decoder As New MetadataDecoder(Me.ContainingPEModule)
            Return decoder.GetTypeSymbolForSerializedType(coClassTypeName)
        End Function

        Friend Overrides ReadOnly Property DefaultPropertyName As String
            Get
                ' Unset value is Nothing. No default member is String.Empty.
                If m_lazyDefaultPropertyName Is Nothing Then
                    Dim memberName = GetDefaultPropertyName()
                    Interlocked.CompareExchange(m_lazyDefaultPropertyName, If(memberName, String.Empty), Nothing)
                End If

                ' Return Nothing rather than String.Empty for no default member.
                Return If(String.IsNullOrEmpty(m_lazyDefaultPropertyName), Nothing, m_lazyDefaultPropertyName)
            End Get
        End Property

        Private Function GetDefaultPropertyName() As String
            Dim memberName As String = Nothing
            ContainingPEModule.Module.HasDefaultMemberAttribute(Me.m_Handle, memberName)

            If memberName IsNot Nothing Then
                For Each member In GetMembers(memberName)
                    ' Allow Default Shared properties for consistency with Dev10.
                    If member.Kind = SymbolKind.Property Then
                        Return memberName
                    End If
                Next
            End If

            Return Nothing
        End Function

        Private Function CreateNestedTypes() As Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))
            Dim members = ArrayBuilder(Of PENamedTypeSymbol).GetInstance()
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each nestedTypeDef In [module].GetNestedTypeDefsOrThrow(m_Handle)
                    If [module].ShouldImportNestedType(nestedTypeDef) Then
                        members.Add(New PENamedTypeSymbol(moduleSymbol, Me, nestedTypeDef))
                    End If
                Next
            Catch mrEx As BadImageFormatException
            End Try

            Dim children = members.GroupBy(Function(t) t.Name, IdentifierComparison.Comparer)
            Dim types = New Dictionary(Of String, ImmutableArray(Of PENamedTypeSymbol))(IdentifierComparison.Comparer)

            For Each c In children
                types.Add(c.Key, c.ToArray().AsImmutableOrNull())
            Next

            members.Free()
            Return types
        End Function

        Private Sub CreateFields(members As ArrayBuilder(Of Symbol),
                                 <Out()> ByRef witheventPropertyNames As HashSet(Of String))

            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module
            Dim isOrdinaryStruct As Boolean = Me.TypeKind = TypeKind.Structure AndAlso
                                                    (Me.SpecialType = SpecialType.None OrElse Me.SpecialType = SpecialType.System_Nullable_T)

            Try
                For Each fieldDef In [module].GetFieldsOfTypeOrThrow(m_Handle)
                    Dim import As Boolean = True

                    Try
                        If Not ([module].ShouldImportField(fieldDef, moduleSymbol.ImportOptions) OrElse
                                isOrdinaryStruct AndAlso ([module].GetFieldDefFlagsOrThrow(fieldDef) And FieldAttributes.Static) = 0) Then
                            import = False
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try

                    If import Then
                        members.Add(New PEFieldSymbol(moduleSymbol, Me, fieldDef))
                    End If

                    Dim witheventPropertyName As String = Nothing
                    If [module].HasAccessedThroughPropertyAttribute(fieldDef, witheventPropertyName) Then
                        '       Dev10 does not check if names are duplicated, but it does check
                        '       that withevents names match some property name using identifier match
                        '       So if names are duplicated they would refer to same property.
                        '       We will just put names in a set.
                        If witheventPropertyNames Is Nothing Then
                            witheventPropertyNames = New HashSet(Of String)(IdentifierComparison.Comparer)
                        End If

                        witheventPropertyNames.Add(witheventPropertyName)
                    End If

                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Function CreateMethods() As Dictionary(Of MethodHandle, PEMethodSymbol)
            Dim methods = New Dictionary(Of MethodHandle, PEMethodSymbol)()
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each methodDef In [module].GetMethodsOfTypeOrThrow(m_Handle)
                    If [module].ShouldImportMethod(methodDef, moduleSymbol.ImportOptions) Then
                        methods.Add(methodDef, New PEMethodSymbol(moduleSymbol, Me, methodDef))
                    End If
                Next
            Catch mrEx As BadImageFormatException
            End Try

            Return methods
        End Function

        Private Sub CreateProperties(methodHandleToSymbol As Dictionary(Of MethodHandle, PEMethodSymbol), members As ArrayBuilder(Of Symbol))
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each propertyDef In [module].GetPropertiesOfTypeOrThrow(m_Handle)
                    Try
                        Dim methods = [module].GetPropertyMethodsOrThrow(propertyDef)


                        Dim getMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, methods.Getter)
                        Dim setMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, methods.Setter)

                        If (getMethod IsNot Nothing) OrElse (setMethod IsNot Nothing) Then
                            members.Add(New PEPropertySymbol(moduleSymbol, Me, propertyDef, getMethod, setMethod))
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try
                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Sub CreateEvents(methodHandleToSymbol As Dictionary(Of MethodHandle, PEMethodSymbol), members As ArrayBuilder(Of Symbol))
            Dim moduleSymbol = Me.ContainingPEModule
            Dim [module] = moduleSymbol.Module

            Try
                For Each eventRid In [module].GetEventsOfTypeOrThrow(m_Handle)
                    Try
                        Dim methods = [module].GetEventMethodsOrThrow(eventRid)

                        ' NOTE: C# ignores all other accessors (most notably, raise/fire).
                        Dim addMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, methods.AddOn)
                        Dim removeMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, methods.RemoveOn)
                        Dim raiseMethod = GetAccessorMethod(moduleSymbol, methodHandleToSymbol, methods.Fire)

                        ' VB ignores events that do not have both Add and Remove.
                        If (addMethod IsNot Nothing) AndAlso (removeMethod IsNot Nothing) Then
                            members.Add(New PEEventSymbol(moduleSymbol, Me, eventRid, addMethod, removeMethod, raiseMethod))
                        End If
                    Catch mrEx As BadImageFormatException
                    End Try
                Next
            Catch mrEx As BadImageFormatException
            End Try
        End Sub

        Private Shared Function GetAccessorMethod(moduleSymbol As PEModuleSymbol, methodHandleToSymbol As Dictionary(Of MethodHandle, PEMethodSymbol), methodDef As MethodHandle) As PEMethodSymbol
            If methodDef.IsNil Then
                Return Nothing
            End If

            Dim method As PEMethodSymbol = Nothing
            Dim found As Boolean = methodHandleToSymbol.TryGetValue(methodDef, method)
            Debug.Assert(found OrElse Not moduleSymbol.Module.ShouldImportMethod(methodDef, moduleSymbol.ImportOptions))
            Return method
        End Function

        Friend Overrides Function GetUseSiteErrorInfo() As DiagnosticInfo

            If m_lazyUseSiteErrorInfo Is ErrorFactory.EmptyErrorInfo Then
                m_lazyUseSiteErrorInfo = CalculateUseSiteErrorInfoImpl()
            End If

            Return m_lazyUseSiteErrorInfo
        End Function

        Private Function CalculateUseSiteErrorInfoImpl() As DiagnosticInfo
            Dim diagnostic = CalculateUseSiteErrorInfo()

            If diagnostic Is Nothing Then

                ' Check if this type Is marked by RequiredAttribute attribute.
                ' If so mark the type as bad, because it relies upon semantics that are not understood by the VB compiler.
                If Me.ContainingPEModule.Module.HasRequiredAttributeAttribute(Me.Handle) Then
                    Return ErrorFactory.ErrorInfo(ERRID.ERR_UnsupportedType1, Me)
                End If

                ' Verify type parameters for containing types
                ' match those on the containing types.
                If Not MatchesContainingTypeParameters() Then
                    Return ErrorFactory.ErrorInfo(ERRID.ERR_NestingViolatesCLS1, Me)
                End If
            End If

            Return diagnostic
        End Function

        ''' <summary>
        ''' Return true if the type parameters specified on the nested type (Me),
        ''' that represent the corresponding type parameters on the containing
        ''' types, in fact match the actual type parameters on the containing types.
        ''' </summary>
        Private Function MatchesContainingTypeParameters() As Boolean
            If m_GenericParameterHandles.Count = 0 Then
                Return True
            End If

            Dim container = ContainingType
            If container Is Nothing Then
                Return True
            End If

            Dim containingTypeParameters = container.GetAllTypeParameters()
            Dim n = containingTypeParameters.Length

            If n = 0 Then
                Return True
            End If

            ' Create an instance of PENamedTypeSymbol for the nested type, but
            ' with all type parameters, from the nested type and all containing
            ' types. The type parameters on this temporary type instance are used
            ' for comparison with those on the actual containing types. The
            ' containing symbol for the temporary type is the namespace directly.
            Dim nestedType = New PENamedTypeSymbol(ContainingPEModule, DirectCast(ContainingNamespace, PENamespaceSymbol), m_Handle)
            Dim nestedTypeParameters = nestedType.TypeParameters
            Dim containingTypeMap = TypeSubstitution.Create(
                container,
                containingTypeParameters,
                IndexedTypeParameterSymbol.Take(n).As(Of TypeSymbol))
            Dim nestedTypeMap = TypeSubstitution.Create(
                nestedType,
                nestedTypeParameters,
                IndexedTypeParameterSymbol.Take(nestedTypeParameters.Length).As(Of TypeSymbol))

            For i = 0 To n - 1
                Dim containingTypeParameter = containingTypeParameters(i)
                Dim nestedTypeParameter = nestedTypeParameters(i)
                If Not MethodSignatureComparer.HaveSameConstraints(
                    containingTypeParameter,
                    containingTypeMap,
                    nestedTypeParameter,
                    nestedTypeMap) Then
                    Return False
                End If
            Next

            Return True
        End Function

        ''' <summary>
        ''' Force all declaration errors to be generated.
        ''' </summary>
        Friend Overrides Sub GenerateDeclarationErrors(cancellationToken As CancellationToken)
            Throw ExceptionUtilities.Unreachable
        End Sub

        Friend Overrides ReadOnly Property ObsoleteAttributeData As ObsoleteAttributeData
            Get
                ObsoleteAttributeHelpers.InitializeObsoleteDataFromMetadata(m_lazyObsoleteAttributeData, m_Handle, ContainingPEModule)
                Return m_lazyObsoleteAttributeData
            End Get
        End Property

        Friend Overrides Function GetAppliedConditionalSymbols() As ImmutableArray(Of String)
            If Me.m_lazyConditionalAttributeSymbols.IsDefault Then
                Dim conditionalSymbols As ImmutableArray(Of String) = ContainingPEModule.Module.GetConditionalAttributeValues(m_Handle)
                Debug.Assert(Not conditionalSymbols.IsDefault)
                ImmutableInterlocked.InterlockedCompareExchange(m_lazyConditionalAttributeSymbols, conditionalSymbols, Nothing)
            End If

            Return Me.m_lazyConditionalAttributeSymbols
        End Function

        Friend Overrides Function GetAttributeUsageInfo() As AttributeUsageInfo
            If m_lazyAttributeUsageInfo.IsNull Then
                m_lazyAttributeUsageInfo = DecodeAttributeUsageInfo()
            End If

            Debug.Assert(Not m_lazyAttributeUsageInfo.IsNull)
            Return m_lazyAttributeUsageInfo
        End Function

        Private Function DecodeAttributeUsageInfo() As AttributeUsageInfo
            Dim attributeUsageHandle = Me.ContainingPEModule.Module.GetAttributeUsageAttributeHandle(m_Handle)
            If Not attributeUsageHandle.IsNil Then
                Dim decoder = New MetadataDecoder(ContainingPEModule)
                Dim positionalArgs As TypedConstant() = Nothing
                Dim namedArgs As KeyValuePair(Of String, TypedConstant)() = Nothing
                If decoder.GetCustomAttribute(attributeUsageHandle, positionalArgs, namedArgs) Then
                    Return AttributeData.DecodeAttributeUsageAttribute(positionalArgs(0), namedArgs.AsImmutableOrNull())
                End If
            End If

            Dim baseType = Me.BaseTypeNoUseSiteDiagnostics
            Return If(baseType IsNot Nothing, baseType.GetAttributeUsageInfo(), AttributeUsageInfo.Default)
        End Function

        Friend Overrides ReadOnly Property HasDeclarativeSecurity As Boolean
            Get
                Throw ExceptionUtilities.Unreachable
            End Get
        End Property

        Friend Overrides Function GetSecurityInformation() As IEnumerable(Of Microsoft.Cci.SecurityAttribute)
            Throw ExceptionUtilities.Unreachable
        End Function

        Friend Overrides ReadOnly Property IsExtensibleInterfaceNoUseSiteDiagnostics As Boolean
            Get
                If m_lazyIsExtensibleInterface = ThreeState.Unknown Then
                    m_lazyIsExtensibleInterface = DecodeIsExtensibleInterface().ToThreeState()
                End If

                Return m_lazyIsExtensibleInterface.Value
            End Get
        End Property

        Private Function DecodeIsExtensibleInterface() As Boolean
            If Me.IsInterfaceType() Then
                If Me.HasAttributeForExtensibleInterface() Then
                    Return True
                End If

                For Each [interface] In Me.AllInterfacesNoUseSiteDiagnostics
                    If [interface].IsExtensibleInterfaceNoUseSiteDiagnostics Then
                        Return True
                    End If
                Next
            End If

            Return False
        End Function

        Private Function HasAttributeForExtensibleInterface() As Boolean
            Dim metadataModule = Me.ContainingPEModule.Module

            ' Is interface marked with 'TypeLibTypeAttribute( flags w/o TypeLibTypeFlags.FNonExtensible )' attribute
            Dim flags As Cci.TypeLibTypeFlags = Nothing
            If metadataModule.HasTypeLibTypeAttribute(Me.m_Handle, flags) AndAlso
                (flags And Cci.TypeLibTypeFlags.FNonExtensible) = 0 Then
                Return True
            End If

            ' Is interface marked with 'InterfaceTypeAttribute( flags with ComInterfaceType.InterfaceIsIDispatch )' attribute
            Dim interfaceType As ComInterfaceType = Nothing
            If metadataModule.HasInterfaceTypeAttribute(Me.m_Handle, interfaceType) AndAlso
                (interfaceType And ComInterfaceType.InterfaceIsIDispatch) <> 0 Then
                Return True
            End If

            Return False
        End Function

        ''' <remarks>
        ''' This is for perf, not for correctness.
        ''' </remarks>
        Friend NotOverridable Overrides ReadOnly Property DeclaringCompilation As VisualBasicCompilation
            Get
                Return Nothing
            End Get
        End Property

        ''' <summary>
        ''' Returns the index of the first member of the specific kind.
        ''' Returns the number of members if not found.
        ''' </summary>
        Private Shared Function GetIndexOfFirstMember(members As ImmutableArray(Of Symbol), kind As SymbolKind) As Integer
            Dim n = members.Length
            For i = 0 To n - 1
                If members(i).Kind = kind Then
                    Return i
                End If
            Next
            Return n
        End Function

        ''' <summary>
        ''' Returns all members of the specific kind, starting at the optional offset.
        ''' Members of the same kind are assumed to be contiguous.
        ''' </summary>
        Private Overloads Shared Iterator Function GetMembers(Of TSymbol As Symbol)(members As ImmutableArray(Of Symbol), kind As SymbolKind, Optional offset As Integer = -1) As IEnumerable(Of TSymbol)
            If offset < 0 Then
                offset = GetIndexOfFirstMember(members, kind)
            End If
            Dim n = members.Length
            For i = offset To n - 1
                Dim member = members(i)
                If member.Kind <> kind Then
                    Return
                End If
                Yield DirectCast(member, TSymbol)
            Next
        End Function

    End Class

End Namespace