package com.loopperfect.buckaroo.tasks;

import com.google.common.base.Preconditions;
import com.google.common.collect.ImmutableList;
import com.google.common.hash.HashCode;
import com.google.common.io.ByteSink;
import com.google.common.io.MoreFiles;
import com.google.common.io.Resources;
import com.loopperfect.buckaroo.*;
import com.loopperfect.buckaroo.buck.BuckFile;
import com.loopperfect.buckaroo.events.*;
import com.loopperfect.buckaroo.serialization.Serializers;
import io.reactivex.Maybe;
import io.reactivex.Observable;
import io.reactivex.Single;
import io.reactivex.schedulers.Schedulers;

import java.io.IOException;
import java.nio.charset.Charset;
import java.nio.file.*;
import java.util.Optional;

public final class CommonTasks {

    private CommonTasks() {
        super();
    }

    public static String toFolderName(final RecipeIdentifier identifier) {
        Preconditions.checkNotNull(identifier);
        return identifier.source.map(x -> x.name + ".").orElse("") +
            identifier.organization + "." + identifier.recipe;
    }

    public static String generateBuckarooDeps(final ImmutableList<ResolvedDependencyReference> dependencies) throws IOException {
        Preconditions.checkNotNull(dependencies);
        return "# Generated by Buckaroo, do not edit! \n" +
            "# This file should not be tracked in source-control. \n" +
            Either.orThrow(BuckFile.list(
                "BUCKAROO_DEPS",
                dependencies.stream()
                    .map(ResolvedDependencyReference::encode)
                    .collect(ImmutableList.toImmutableList())));
    }

    public static Single<String> readFile(final Path path) {
        return Single.fromCallable(() -> EvenMoreFiles.read(path));
    }

    public static Single<ReadProjectFileEvent> readProjectFile(final Path path) {
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() ->
            Either.orThrow(Serializers.parseProject(EvenMoreFiles.read(path))))
            .map(ReadProjectFileEvent::of);
    }

    public static Single<DependencyLocks> readLockFile(final Path path) {
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() ->
            Either.orThrow(Serializers.parseDependencyLocks(EvenMoreFiles.read(path))));
    }

    public static Single<WriteFileEvent> writeFile(final String content, final Path path, final boolean overwrite) {
        Preconditions.checkNotNull(content);
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() -> {
            if (path.getParent() != null && !Files.exists(path.getParent())) {
                Files.createDirectories(path.getParent());
            }
            if (overwrite) {
                Files.deleteIfExists(path);
            } else if (Files.isDirectory(path)) {
                throw new IOException("There is already a directory at " + path);
            } else if (Files.exists(path)) {
                throw new IOException("There is already a file at " + path);
            }
            final ByteSink sink = MoreFiles.asByteSink(path);
            sink.write(content.getBytes());
            return WriteFileEvent.of(path, content.length() < 1024 ? Optional.of(content) : Optional.empty());
        });
    }

    public static Single<WriteFileEvent> writeFile(final String content, final Path path) {
        return writeFile(content, path, false);
    }

    public static Single<TouchFileEvent> touchFile(final Path path) {
        return Single.fromCallable(() -> {
            MoreFiles.touch(path);
            return TouchFileEvent.of(path);
        });
    }

    public static Single<DeleteFileIfExistsEvent> deleteIfExists(final Path path) {
        return Single.fromCallable(() -> {
            final boolean somethingWasDeleted = Files.deleteIfExists(path);
            if (somethingWasDeleted) {
                return DeleteFileIfExistsEvent.of(path);
            }
            return DeleteFileIfExistsEvent.of(path, false);
        });
    }

    public static Single<CreateDirectoryEvent> createDirectory(final Path path) {
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() -> {
            Files.createDirectories(path);
            return CreateDirectoryEvent.of(path);
        });
    }

    public static Single<FileCopyEvent> copy(final Path source, final Path destination, CopyOption... copyOptions) {
        Preconditions.checkNotNull(source);
        Preconditions.checkNotNull(destination);
        return Single.fromCallable(() -> {
            Files.copy(source, destination, copyOptions);
            return FileCopyEvent.of(source, destination);
        });
    }

    public static Single<FileUnzipEvent> unzip(final Path source, final Path target, final Optional<Path> subPath, CopyOption... copyOptions) {
        Preconditions.checkNotNull(source);
        Preconditions.checkNotNull(target);
        Preconditions.checkNotNull(subPath);
        return Single.fromCallable(() -> {
            EvenMoreFiles.unzip(source, target, subPath, copyOptions);
            return FileUnzipEvent.of(source, target);
        });
    }

    public static Single<Recipe> readRecipeFile(final Path path) {
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() ->
            Either.orThrow(Serializers.parseRecipe(EvenMoreFiles.read(path))));
    }

    public static Single<ReadConfigFileEvent> readConfigFile(final Path path) {
        Preconditions.checkNotNull(path);
        return Single.fromCallable(() ->
            Either.orThrow(Serializers.parseConfig(EvenMoreFiles.read(path))))
            .map(ReadConfigFileEvent::of);
    }

    public static Single<ReadConfigFileEvent> readAndMaybeGenerateConfigFile(final FileSystem fs) {
        Preconditions.checkNotNull(fs);
        return Single.fromCallable(() -> {
            final Path configFilePath = fs.getPath(
                System.getProperty("user.home"),
                ".buckaroo",
                "buckaroo.json");
            if (!Files.exists(configFilePath)) {
                final String defaulConfigString = Resources.toString(
                    Resources.getResource("com.loopperfect.buckaroo/DefaultConfig.txt"),
                    Charset.defaultCharset());
                EvenMoreFiles.writeFile(configFilePath, defaulConfigString);
                return Either.orThrow(Serializers.parseConfig(defaulConfigString));
            }
            return Either.orThrow(Serializers.parseConfig(EvenMoreFiles.read(configFilePath)));
        }).map(ReadConfigFileEvent::of);
    }

    public static Single<FileHashEvent> hash(final Path path) {

        Preconditions.checkNotNull(path);

        return Single.fromCallable(() -> {
            final HashCode hash = EvenMoreFiles.hashFile(path);
            return FileHashEvent.of(path, hash);
        });
    }

    /**
     * Verifies the hash of a given file.
     *
     * If the check succeeds, then the Observable will complete.
     *
     * Progress in running the check is reported by the Observable.
     *
     * @param path
     * @param expected
     * @return
     */
    public static Observable<Event> ensureHash(final Path path, final HashCode expected) {

        Preconditions.checkNotNull(path);
        Preconditions.checkNotNull(expected);

        return hash(path).flatMapObservable(
            event -> event.sha256.equals(expected) ?
                Observable.empty() :
                Observable.error(new HashMismatchException(expected, event.sha256)));
    }

    public static Observable<Event> downloadRemoteFile(final FileSystem fs, final RemoteFile remoteFile, final Path target) {

        Preconditions.checkNotNull(fs);
        Preconditions.checkNotNull(remoteFile);
        Preconditions.checkNotNull(target);

        return Observable.concat(

            // Does the file exist?
            Observable.fromCallable(() -> Files.exists(target))
                .flatMap(
                    exists -> {
                        if (exists) {
                            // Then skip the download
                            return Observable.empty();
                        }
                        // Otherwise, download the file
                        return DownloadTask.download(remoteFile.url, target).subscribeOn(Schedulers.io());
                    }).cast(Event.class),

            // Verify the hash
            ensureHash(target, remoteFile.sha256)
        );
    }

    public static Observable<Event> downloadRemoteArchive(final FileSystem fs, final RemoteArchive remoteArchive, final Path targetDirectory) {

        Preconditions.checkNotNull(fs);
        Preconditions.checkNotNull(remoteArchive);
        Preconditions.checkNotNull(targetDirectory);

        final Path zipFilePath = targetDirectory.getParent().resolve(targetDirectory.getFileName() + ".zip");

        return Observable.concat(

            // Download the file
            CommonTasks.downloadRemoteFile(fs, remoteArchive.asRemoteFile(), zipFilePath),

            // Unpack the zip
            MoreCompletables.fromRunnable(() -> {
                EvenMoreFiles.unzip(
                    zipFilePath,
                    targetDirectory,
                    remoteArchive.subPath.map(subPath -> fs.getPath(fs.getSeparator(), subPath)),
                    StandardCopyOption.REPLACE_EXISTING);
            }).toObservable());
    }
}
