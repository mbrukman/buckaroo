module Tests

open System
open Xunit

open Constraint
open Dependency
open Manifest

[<Fact>]
let ``SemVer.parse works correctly`` () =
  let cases = [
    ("7", Some { SemVer.zero with Major = 7 });
    ("6.4", Some { SemVer.zero with Major = 6; Minor = 4 });
    ("1.2.3", Some { SemVer.zero with Major = 1; Minor = 2; Patch = 3 });
    ("  1.2.3  ", Some { SemVer.zero with Major = 1; Minor = 2; Patch = 3 });
    (" 4.5.6.78", Some { Major = 4; Minor = 5; Patch = 6; Increment = 78 });
    ("v1.2.3", Some { SemVer.zero with Major = 1; Minor = 2; Patch = 3 });
    ("V4.2.7", Some { SemVer.zero with Major = 4; Minor = 2; Patch = 7 });
    ("", None);
    ("abc", None);
  ]
  for (input, expected) in cases do
    Assert.Equal(expected, SemVer.parse input)

[<Fact>]
let ``SemVer.compare works correctly`` () =
  Assert.True(SemVer.compare SemVer.zero SemVer.zero = 0)
  Assert.True(SemVer.compare { SemVer.zero with Major = 1 } SemVer.zero = 1)
  Assert.True(SemVer.compare { SemVer.zero with Major = 1; Minor = 2 } { SemVer.zero with Major = 1 } = 1)

[<Fact>]
let ``Version.parse works correctly`` () =
  let cases = [
    ("tag=abc", Version.Tag "abc" |> Some);
    ("branch=master", Version.Branch "master" |> Some);
    ("revision=aabbccddee", Version.Revision "aabbccddee" |> Some);
    ("1.2", Version.SemVerVersion { SemVer.zero with Major = 1; Minor = 2 } |> Some);
    ("", None)
  ]
  for (input, expected) in cases do
    Assert.Equal(expected, Version.parse input)

[<Fact>]
let ``Constraint.parse works correctly`` () =
  let cases = [
    ("*", Constraint.wildcard |> Some); 
    ("revision=aabbccddee", "aabbccddee" |> Version.Revision |> Exactly |> Some); 
    ("!*", Constraint.wildcard |> Constraint.Complement |> Some); 
    ("any(branch=master)", Some(Any [Exactly (Version.Branch "master")])); 
    ("any(revision=aabbccddee branch=master)", Some(Any [Exactly (Version.Revision "aabbccddee"); Exactly (Version.Branch "master")])); 
    ("all(*)", Some(All [Constraint.wildcard])); 
    ("", None); 
  ]
  for (input, expected) in cases do
    Assert.Equal(expected, Constraint.parse input)

[<Fact>]
let ``Constraint.satisfies works correctly`` () =
  let v = Version.Revision "aabbccddee"
  let w = Version.Tag "rc1"
  let c = Constraint.Exactly v
  Assert.True(Constraint.satisfies c v)
  Assert.False(Constraint.satisfies c w)

[<Fact>]
let ``Dependency.parse works correctly`` () =
  let p = Project.GitHub { Owner = "abc"; Project = "def" }
  let cases = [
    ("github.com/abc/def@*", Some({ Project = p; Constraint = Constraint.wildcard }))
    ("", None); 
  ]
  for (input, expected) in cases do
    Assert.Equal(expected, Dependency.parse input)

[<Fact>]
let ``ResolvedVersion.isCompatible works correctly`` () =
  let v = Version.Branch "master"
  let w = Version.Tag "rc1"
  let r = "aabbccddee"
  let s = "llmmnnoopp"
  Assert.True(ResolvedVersion.isCompatible { Version = v; Revision = r } { Version = v; Revision = r })
  Assert.True(ResolvedVersion.isCompatible { Version = v; Revision = r } { Version = w; Revision = r })
  Assert.True(ResolvedVersion.isCompatible { Version = v; Revision = r } { Version = v; Revision = s })
  Assert.False(ResolvedVersion.isCompatible { Version = v; Revision = r } { Version = w; Revision = s })

[<Fact>]
let ``Project.parse works correctly`` () = 
  let cases = [
    ("github.com/abc/def", Project.GitHub { Owner = "abc"; Project = "def" } |> Some);
    ("github+abc/def", Project.GitHub { Owner = "abc"; Project = "def" } |> Some);
    ("", None)
  ]
  for (input, expected) in cases do
    Assert.Equal(expected, Project.parse input)

// [<Fact>]
// let ``Manifest.parse works correctly`` () =
//   let content = "{ \"dependencies\": { \"github+njlr/test-lib-b\": \"*\" } }"
//   let manifest = 
//     { 
//       Dependencies = 
//         [ 
//           { 
//             Project = Project.GitHub { Owner = "njlr"; Project = "test-lib-b" }; 
//             Constraint = Constraint.Wildcard 
//           } 
//         ] 
//     }
//   Assert.True(Manifest.parse content = Some manifest)
