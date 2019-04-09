var browserify = require('browserify'),
    watchify = require('watchify'),
    gulp = require('gulp'),
    merge = require('merge-stream'),
    file = require('gulp-file'),
    jsonEditor = require("gulp-json-editor"),
    del = require('del'),
    source = require('vinyl-source-stream'),
    fs = require('fs'),
    sourceFile = './frame.js',
    destFolder = './build/',
    destFile = 'frame.bundle.js';

gulp.task('clean', ['version'], function () {
    return del(destFolder);
});

gulp.task('version', function () {
    let version = fs.readFileSync('../../../version.txt', 'utf8');
    let versionMetadata = fs.readFileSync('../../../version-metadata.txt');
    let fullVersion = version;
    if (versionMetadata) 
        fullVersion = version + '-' + versionMetadata;

    var versionJson = `{ "base": "${version}", "metadata": "${versionMetadata}", "full": "${fullVersion}" }`;

    let manifestPipe = gulp.src("./manifest.json")
        .pipe(jsonEditor({
            'version': version
        }))
        .pipe(gulp.dest('.'))

    let versionPipe = file('version.json', versionJson, {src: true})
        .pipe(gulp.dest('.'));

    return merge(manifestPipe, versionPipe);
});

gulp.task('copy', ['version', 'clean'], function () {
    return gulp.src([
            './*.json',
            './*.png',
            './*.js', 
            '!gulpfile.js',
            './*.html',

            'semantic/dist/semantic.min.css'
        ]).pipe(gulp.dest(destFolder));
});

gulp.task('browserify', ['version', 'clean', 'copy'], function() {
    return browserify(sourceFile)
        .bundle()
        .pipe(source(destFile))
        .pipe(gulp.dest(destFolder));
});

gulp.task('default', ['version', 'clean', 'copy', 'browserify']);