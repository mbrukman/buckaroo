module Buckaroo.Tests.Lock

open Xunit
open Buckaroo

[<Fact>]
let ``Lock.parse works correctly 1`` () = 
  let actual = 
    [
      "manifest = \"aabbccddee\""; 
    ]
    |> String.concat "\n"
    |> Lock.parse
  
  let expected = {
    ManifestHash = "aabbccddee"; 
    Dependencies = Set.empty; 
    Packages = Map.empty; 
  }
  
  Assert.Equal(Result.Ok expected, actual)

[<Fact>]
let ``Lock.parse works correctly 2`` () = 
  let actual = 
    [
      "manifest = \"aabbccddee\""; 
      ""; 
      "[[dependency]]"; 
      "package = \"abc/def\"";
      "target = \"//:def\""; 
      ""; 
      "[lock.\"abc/def\"]";
      "url = \"https://www.abc.com/def.zip\""; 
      "version = \"1.2.3\""; 
      "sha256 = \"aabbccddee\""; 
    ]
    |> String.concat "\n"
    |> Lock.parse
  
  let expected = {
    ManifestHash = "aabbccddee"; 
    Dependencies = 
      [
        {
          Package = PackageIdentifier.Adhoc { Owner = "abc"; Project = "def" }; 
          Target = {
            Folders = []; 
            Name = "def"; 
          }
        }
      ]
      |> Set.ofSeq; 
    Packages = 
      Map.empty
      |> Map.add 
        (PackageIdentifier.Adhoc { Owner = "abc"; Project = "def" }) 
        {
          Version = Version.SemVerVersion { SemVer.zero with Major = 1; Minor = 2; Patch = 3 }; 
          Location = PackageLocation.Http {
            Url = "https://www.abc.com/def.zip"; 
            StripPrefix = None; 
            Type = None; 
            Sha256 = "aabbccddee"; 
          };
          PrivatePackages = Map.empty; 
        }; 
  }
  
  Assert.Equal(Result.Ok expected, actual)

[<Fact>]
let ``Lock.parse works correctly 3`` () = 
  let actual = 
    [
      "manifest = \"aabbccddee\""; 
      ""; 
      "[[dependency]]"; 
      "package = \"abc/def\"";
      "target = \"//:def\""; 
      ""; 
      "[lock.\"abc/def\"]";
      "url = \"https://www.abc.com/def.zip\""; 
      "version = \"1.2.3\""; 
      "sha256 = \"aabbccddee\""; 
      ""; 
      "[lock.\"abc/def\".lock.\"ijk/xyz\"]"; 
      "url = \"https://www.ijk.com/xyz.zip\""; 
      "version = \"1\""; 
      "sha256 = \"aabbccddee\""; 
      ""; 
    ]
    |> String.concat "\n"
    |> Lock.parse
  
  let expected = {
    ManifestHash = "aabbccddee"; 
    Dependencies = 
      [
        {
          Package = PackageIdentifier.Adhoc { Owner = "abc"; Project = "def" }; 
          Target = {
            Folders = []; 
            Name = "def"; 
          }
        }
      ]
      |> Set.ofSeq; 
    Packages = 
      Map.empty
      |> Map.add 
        (PackageIdentifier.Adhoc { Owner = "abc"; Project = "def" }) 
        {
          Version = Version.SemVerVersion { SemVer.zero with Major = 1; Minor = 2; Patch = 3 }; 
          Location = PackageLocation.Http {
            Url = "https://www.abc.com/def.zip"; 
            StripPrefix = None; 
            Type = None; 
            Sha256 = "aabbccddee"; 
          };
          PrivatePackages = 
            Map.empty
            |> Map.add 
              (PackageIdentifier.Adhoc { Owner = "ijk"; Project = "xyz" }) 
              {
                Version = Version.SemVerVersion { SemVer.zero with Major = 1; }; 
                Location = PackageLocation.Http {
                  Url = "https://www.ijk.com/xyz.zip"; 
                  StripPrefix = None; 
                  Type = None; 
                  Sha256 = "aabbccddee"; 
                };
                PrivatePackages = Map.empty; 
              }; 
        }; 
  }
  
  Assert.Equal(Result.Ok expected, actual)