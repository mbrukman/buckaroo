namespace Buckaroo

type Lock = {
  ManifestHash : string; 
  Dependencies : Set<TargetIdentifier>; 
  Packages : Map<PackageIdentifier, Version * PackageLocation>; 
}

module Lock = 

  open Buckaroo.Result
  open System.Security.Cryptography

  let showDiff (before : Lock) (after : Lock) : string = 
    let additions = 
      after.Packages
      |> Seq.filter (fun x -> before.Packages |> Map.containsKey x.Key |> not)
      |> Seq.distinct
    let removals = 
      before.Packages
      |> Seq.filter (fun x -> after.Packages |> Map.containsKey x.Key |> not)
      |> Seq.distinct
    let changes = 
      before.Packages
      |> Seq.choose (fun x -> 
        after.Packages 
        |> Map.tryFind x.Key 
        |> Option.bind (fun y -> 
          if y <> x.Value
          then Some (x.Key, y, x.Value)
          else None
        )
      )
    [
      "Added: "; 
      (
        additions 
        |> Seq.map (fun x -> 
          "  " + (PackageIdentifier.show x.Key) + 
          " -> " + (PackageLocation.show (snd x.Value))
        )
        |> String.concat "\n"
      );
      "Removed: "; 
      (
        removals 
        |> Seq.map (fun x -> 
          "  " + (PackageIdentifier.show x.Key) + 
          " -> " + (PackageLocation.show (snd x.Value))
        )
        |> String.concat "\n"
      );
      "Changed: "; 
      (
        changes 
        |> Seq.map (fun (p, before, after) -> 
          "  " + (PackageIdentifier.show p) + 
          " " + (PackageLocation.show (snd before)) + 
          " -> " + (PackageLocation.show (snd after))
        )
        |> String.concat "\n"
      );
    ]
    |> String.concat "\n"

  let bytesToHex bytes = 
    bytes 
    |> Array.map (fun (x : byte) -> System.String.Format("{0:x2}", x))
    |> String.concat System.String.Empty

  let fromManifestAndSolution (manifest : Manifest) (solution : Solution) : Lock = 
    let manifestHash = 
      manifest
      |> Manifest.toToml
      |> System.Text.Encoding.UTF8.GetBytes 
      |> (new SHA256Managed()).ComputeHash 
      |> bytesToHex
    let dependencies = 
      manifest.Dependencies
      |> Seq.map (fun x -> x.Package)
      |> Seq.collect (fun p -> 
        solution 
        |> Map.tryFind p 
        |> Option.map (fun x -> 
          x.Manifest.Targets 
          |> Seq.map (fun t -> { Package = p; Target = t})
        )
        |> Option.defaultValue Seq.empty
      )
      |> Set.ofSeq
    let packages = 
      solution
      |> Map.map (fun _ v -> (v.Version, v.Location))
    { ManifestHash = manifestHash; Dependencies = dependencies; Packages = packages }

  let toToml (lock : Lock) = 
    (
       "manifest = \"" + lock.ManifestHash + "\"\n\n"
    ) + 
    (
      lock.Dependencies
      |> Seq.map(fun x -> 
        "[[dependency]]\n" + 
        "package = \"" + (PackageIdentifier.show x.Package) + "\"\n" + 
        "target = \"" + (Target.show x.Target) + "\"\n\n" 
      )
      |> String.concat ""
    ) + 
    (
      lock.Packages
      |> Seq.map(fun x -> 
        let package = x.Key
        let (version, exactLocation) = x.Value
        "[[lock]]\n" + 
        "name = \"" + (PackageIdentifier.show package) + "\"\n" + 
        "version = \"" + (Version.show version) + "\"\n" + 
        match exactLocation with 
        | Git git -> 
          "url = \"" + git.Url + "\"\n" + 
          "revision = \"" + git.Revision + "\"\n"
        | Http http -> 
          "url = \"" + http.Url + "\"\n" + 
          "sha256 = \"" + (http.Sha256) + "\"\n" + 
          (http.Type |> Option.map (fun x -> "type = \"" + (ArchiveType.show x) + "\"\n") |> Option.defaultValue "") + 
          (http.StripPrefix |> Option.map (fun x -> "strip_prefix = \"" + x + "\"\n") |> Option.defaultValue "")
        | GitHub gitHub -> 
          "revision = \"" + gitHub.Revision + "\"\n"
      )
      |> String.concat "\n"
    )

  let private tomlTableToLockedHttpPackage (x : Nett.TomlTable) = result {
    let! packageIdentifier = 
      x 
      |> Toml.get "name" 
      |> Option.bind Toml.asString 
      |> optionToResult "name must be specified for every dependency"
      |> Result.bind PackageIdentifier.parseAdhocIdentifier
    
    let! version = 
      x 
      |> Toml.get "version" 
      |> Option.bind Toml.asString 
      |> optionToResult "version must be specified for every dependency"
      |> Result.bind Version.parse
    
    let! url = 
      x
      |> Toml.get "url"
      |> Option.bind Toml.asString 
      |> optionToResult "url must be specified for every dependency"

    let! sha256 = 
      x
      |> Toml.get "sha256"
      |> Option.bind Toml.asString 
      |> optionToResult "sha256 must be specified for every dependency"
    
    let! stripPrefix = 
      x
      |> Toml.get "strip_prefix"
      |> Option.map (fun element -> 
        element 
        |> Toml.asString
        |> optionToResult "strip_prefix must be a string"
        |> Result.map Option.Some
      )
      |> Option.defaultValue (Result.Ok Option.None)

    let! archiveType = 
      x
      |> Toml.get "type"
      |> Option.map (fun element -> 
        element 
        |> Toml.asString
        |> optionToResult "type must be a string"
        |> Result.bind (ArchiveType.parse >> (Result.mapError ArchiveType.ParseError.show))
        |> Result.map Option.Some
      )
      |> Option.defaultValue (Result.Ok Option.None)

    let location = PackageLocation.Http {
      Url = url; 
      StripPrefix = stripPrefix; 
      Type = archiveType; 
      Sha256 = sha256; 
    }

    return (PackageIdentifier.Adhoc packageIdentifier, (version, location))
  }

  let private tomlTableToLockedGitHubPackage x = result {
    let! packageIdentifier = 
      x 
      |> Toml.get "name" 
      |> Option.bind Toml.asString 
      |> optionToResult "name must be specified for every dependency"
      |> Result.bind PackageIdentifier.parseGitHubIdentifier
    
    let! version = 
      x 
      |> Toml.get "version" 
      |> Option.bind Toml.asString 
      |> optionToResult "version must be specified for every dependency"
      |> Result.bind Version.parse

    let! revision = 
      x 
      |> Toml.get "revision" 
      |> Option.bind Toml.asString 
      |> optionToResult "revision must be specified for every dependency"

    let hint = 
      match version with
      | Version.Branch b -> Hint.Branch b
      | Version.Tag  t -> Hint.Tag t
      | _ -> Hint.Default

    let packageLocation = 
      PackageLocation.GitHub
        {
          Package = packageIdentifier; 
          Revision = revision; 
          Hint = hint; 
        }

    return (PackageIdentifier.GitHub packageIdentifier, (version, packageLocation))
  }

  let private tomlTableToLockedPackage (x : Nett.TomlTable) : Result<(PackageIdentifier * (Version * PackageLocation)), string> = result {
    if x |> Toml.get "url" |> Option.isSome
    then
      return! tomlTableToLockedHttpPackage x
    else 
      return! tomlTableToLockedGitHubPackage x
  }

  let tomlTableToTargetIdentifier (x : Nett.TomlTable) : Result<TargetIdentifier, string> = result {
    let! package = 
      x 
      |> Toml.get "package" 
      |> Option.bind Toml.asString 
      |> optionToResult "package must be specified for every dependency"
      |> Result.bind PackageIdentifier.parse
    let! target = 
      x 
      |> Toml.get "target" 
      |> Option.bind Toml.asString 
      |> optionToResult "target must be specified for every dependency"
      |> Result.bind Target.parse
    return { Package = package; Target = target }
  }

  let parse (content : string) : Result<Lock, string> = result {
    let! table = Toml.parse content |> Result.mapError (fun e -> e.Message)
    let! manifestHash = 
      table 
      |> Toml.get "manifest"
      |> Option.bind Toml.asString 
      |> optionToResult "manifest hash must be specified"
    let! lockedPackages = 
      table.Rows
      |> Seq.filter (fun x -> x.Key = "lock")
      |> Seq.choose (fun x -> Toml.asTableArray x.Value)
      |> Seq.collect (fun x -> x.Items)
      |> Seq.map tomlTableToLockedPackage
      |> Result.all
    // TODO: If a project has more than one revision or location throw an error
    let packages = 
      lockedPackages
      |> Map.ofSeq
    let! dependencies = 
      table.Rows
      |> Seq.filter (fun x -> x.Key = "dependency")
      |> Seq.choose (fun x -> Toml.asTableArray x.Value)
      |> Seq.collect (fun x -> x.Items)
      |> Seq.map tomlTableToTargetIdentifier
      |> Result.all
    
    return { 
      ManifestHash = manifestHash; 
      Dependencies = set dependencies; 
      Packages = packages; 
    }
  }