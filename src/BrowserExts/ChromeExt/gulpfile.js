var browserify = require('browserify'),
    watchify = require('watchify'),
    gulp = require('gulp'),
    del = require('del'),
    source = require('vinyl-source-stream'),
    sourceFile = './frame.js',
    destFolder = './build/',
    destFile = 'frame.bundle.js';

gulp.task('clean', function () {
    return del(destFolder);
});

gulp.task('copy', ['clean'], function () {
    return gulp.src([
            './manifest.json',
            './*.png',
            './*.js', 
            '!gulpfile.js',
            './*.html',

            'semantic/dist/semantic.min.css'
        ]).pipe(gulp.dest(destFolder));
});

gulp.task('browserify', ['clean', 'copy'], function() {
    return browserify(sourceFile)
        .bundle()
        .pipe(source(destFile))
        .pipe(gulp.dest(destFolder));
});

gulp.task('default', ['clean', 'copy', 'browserify']);