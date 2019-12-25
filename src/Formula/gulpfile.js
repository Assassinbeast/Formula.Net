//Main 
var gulp = require('gulp');
var watch = require("gulp-watch");
var sourcemaps = require('gulp-sourcemaps');

//Tools
var fs = require('fs');
var path = require('path');
var del = require('del');
var rimraf = require('rimraf');
var chalk = require("chalk");
var browserSync = require('browser-sync').create();
var gulpif = require('gulp-if');

//ts
var ts = require('gulp-typescript');
//Notes: gulp-uglify is the least buggt than gulp-uglifly and gulp-minify when setting breakpoints and stepping through in chrome
var minifyJS = require('gulp-uglify'); //TODO: Makes browser not able to debug and set breakpoints

//scss
var sass = require("gulp-sass");
var cssAutoPrefixer = require("gulp-autoprefixer");
var minifyCSS = require('gulp-clean-css');
var stripCssComments = require('gulp-strip-css-comments');

//Configuration
var proxyUrl = "identhorize.com.me";
var currentCompileCount = 0;
const timeout = ms => new Promise(res => setTimeout(res, ms));
var shallMinifyJs = false; //Might have problems setting breakpoints and stepthrough chrome if true
var typescriptCompileTarget = "ES5";

gulp.task("test", async function (done) {
    setTimeout(() => {
        console.log("grr");
    }, 1000);

    console.log("hmm1");
    await timeout(2000);
    console.log("hmm2");

    done();

});

gulp.task("run", async function (done) {
    await start(true, true, false);
});

//Helpers
String.prototype.replaceAll = function (search, replacement) {
    var target = this;
    return target.split(search).join(replacement);
};
function getDirs(rootPath, returnOnlyName = true) {
    if (fs.existsSync(rootPath) === false)
        return [];

    var dirs = fs.readdirSync(rootPath)
        .filter(f => fs.statSync(path.join(rootPath, f)).isDirectory());
    if (returnOnlyName)
        return dirs;

    for (var i = 0; i < dirs.length; i++)
        dirs[i] = textTrimStart(rootPath, "/") + "/" + dirs[i];
    return dirs;
}
function getDirsAndSubDirs(rootDir) {
    function getAllDirInDir2(dirsArray, fromDir) {
        var dirs = getDirs(fromDir);
        for (var i = 0; i < dirs.length; i++) {
            dirsArray.push(fromDir + "/" + dirs[i]);
            getAllDirInDir2(dirsArray, fromDir + "/" + dirs[i]);
        }
    }

    var dirsArray = [];
    getAllDirInDir2(dirsArray, rootDir);
    return dirsArray;
}
function isPathAFile(path) {
    var pathItems = path.split("/");
    if (pathItems[pathItems.length - 1].includes("."))
        return true;
    return false;
}
function getDirectoryPath(pathOrFile) {
    //pathOrFile could be "pages/account" or "pages/account/foo.ts"
    //So we have to find out if its a file or directory
    var pathItems = pathOrFile.split("/");

    //if file
    if (pathItems[pathItems.length - 1].includes("."))
        pathItems.pop();

    return pathItems.join("/");
}
function getFileName(filePath) {
    var pathItems = filePath.split("/");
    return pathItems[pathItems.length - 1];
}
function getFileExtension(file) {
    var fileItems = file.split(".");
    return fileItems[fileItems.length - 1];
}
function getFilesInDir(dirPath) {
    var files = [];
    var filesAndDirs = fs.readdirSync(dirPath);
    for (var i = 0; i < filesAndDirs.length; i++) {
        if (isPathAFile(filesAndDirs[i]) === false)
            continue;
        files.push(filesAndDirs[i]);
    }
    return files;
}
function isFileDTs(filePath) {
    if (filePath.length < 5)
        return false;
    if (filePath.substr(filePath.length - 5, 5) === ".d.ts")
        return true;
    return false;
}
function renameToFileType(filePath, fileType) {
    var fileDotItems = filePath.split(".");
    fileDotItems.pop();
    return fileDotItems.join(".") + "." + fileType;
}
function textTrimStart(text, charToTrim) {
    var t = true;
    while (t) {
        if (text.substr(0, 1) === charToTrim[0]) {
            text = text.substr(1);
            continue;
        }
        break;
    }
    return text;
}
function textTrimEnd(text, charToTrim) {
    var t = true;
    while (t) {
        if (text.substr(text.length - 1, 1) === charToTrim[0]) {
            text = text.substr(0, text.length - 1);
            continue;
        }
        break;
    }
    return text;
}
function transformAbsolutePathToRelativePath(absPath) {
    //absPath eg: C:\Users\Danny\Desktop\Main\Projects\JsonCraft\JsonCraft\gulpfile.js
    //__dirname is eg: C:\Users\Danny\Desktop\Main\Projects\JsonCraft\JsonCraft

    var relativePath = absPath.substr(__dirname.length);
    return textTrimStart(textTrimStart(relativePath, "\\"), "/");
}

//Misc actions
function log(message, ...optionalParameters) {
    var date = new Date();
    var hours = date.getHours().toString().length === 1 ? "0" + date.getHours() : date.getHours();
    var minutes = date.getMinutes().toString().length === 1 ? "0" + date.getMinutes() : date.getMinutes();
    var seconds = date.getSeconds().toString().length === 1 ? "0" + date.getSeconds() : date.getSeconds();
    var x = "[" + chalk.gray(hours + ":" + minutes + ":" + seconds) + "]";
    optionalParameters.unshift(message);
    console.log(x, ...optionalParameters);
}
function deleteFileInWWWRoot(filePath) {
    filePath = filePath.replaceAll("\\", "/");
    filePath = textTrimStart(filePath, "/");
    del.sync("wwwroot/" + filePath);
    log("Deleted " + chalk.red("wwwroot/" + filePath));
}
function deleteAllFilesInDir(dirPath) {
    if (fs.existsSync(dirPath) === false)
        return;
    var files = getFilesInDir(dirPath);
    for (var i = 0; i < files.length; i++) {
        var file = files[i].replaceAll("\\", "/");
        var fullFilePath = textTrimEnd(dirPath, "/") + "/" + textTrimStart(file, "/");
        console.log(fullFilePath);
        deleteFileInWWWRoot(fullFilePath.substr(8));
    }
}
function deleteAllFilesInDest() {
    rimraf.sync('wwwroot/app');
    log("Deleted " + chalk.red("wwwroot/app"));
    rimraf.sync('wwwroot/layouts');
    log("Deleted " + chalk.red("wwwroot/layouts"));
    rimraf.sync('wwwroot/pages');
    log("Deleted " + chalk.red("wwwroot/pages"));
    rimraf.sync('wwwroot/webobjects');
    log("Deleted " + chalk.red("wwwroot/webobjects"));
    rimraf.sync('wwwroot/formula');
    log("Deleted " + chalk.red("wwwroot/formula"));
}

//Compiling
function compileTypescripts(dirPath) {
    dirPath = dirPath.replaceAll("\\", "/"); //"webobjects/Calendar"

    var files = getFilesInDir(dirPath);
    for (var i = 0; i < files.length; i++) {
        var fileExtension = getFileExtension(files[i]);
        if (fileExtension === "ts" || fileExtension === "js")
            compileTypescript(dirPath + "/" + files[i]);
    }
}
function compileSasses(dirPath) {
    dirPath = dirPath.replaceAll("\\", "/"); //"webobjects/Calendar"
    var files = getFilesInDir(dirPath);
    for (var i = 0; i < files.length; i++) {
        var fileExtension = getFileExtension(files[i]);
        if (fileExtension === "scss" || fileExtension === "css")
            compileSass(dirPath + "/" + files[i]);
    }
}
function compileSass(filePath) {
    filePath = filePath.replaceAll("\\", "/").toLowerCase(); // "pages/account" or "pages/account/foo.scss"
    var dirPath = getDirectoryPath(filePath).toLowerCase(); //eg "Pages/Account/Settings"
    var dest = "wwwroot/" + dirPath;

    var startMilliseconds = Date.now();
    log("Compiling " + chalk.yellowBright(filePath));
    currentCompileCount++;
    var stream = gulp.src(filePath)
        .pipe(sourcemaps.init())
        .pipe(sass())
        .on('error', function (err) {
            log(err.toString());
            this.emit('end');
        })
        .pipe(stripCssComments("/*!", { preserve: false }))
        .pipe(cssAutoPrefixer({grid: "autoplace"}))
        .pipe(minifyCSS())
        .pipe(sourcemaps.write("./", {
            sourceRoot: "./", sourceMappingURL: function (file) {
                return "/" + dirPath + "/" + file.sourceMap.file + ".map";
            }
        }))
        .pipe(gulp.dest(dest));

    stream.on('end', function () {
        var timePassed = Date.now() - startMilliseconds;
        log("Finished compiling " + chalk.green(filePath) + " - took " + timePassed / 1000 + " seconds");
        currentCompileCount--;
    });

    return stream;
}
function compileTypescript(filePath) {
    var fileExt = getFileExtension(filePath);
    if ((fileExt === "ts" || fileExt === "js") === false ||
        isFileDTs(filePath))
        return;

    filePath = filePath.replaceAll("\\", "/").toLowerCase(); //"layouts/Main/Main.ts"

    var dirPath = getDirectoryPath(filePath).toLowerCase(); //layouts/Main
    var dest = "wwwroot/" + dirPath;

    var startMilliseconds = Date.now();
    log("Compiling " + chalk.yellowBright(filePath));
    currentCompileCount++;
    var stream = gulp.src(filePath)
        .pipe(sourcemaps.init())
        .pipe(ts({ allowJs: true, isolatedModules: true, target: typescriptCompileTarget }))
        .on("error", function (err) {
            log(err.toString());
            this.emit('end');
        })
        .pipe(gulpif(shallMinifyJs, minifyJS()))
        .pipe(sourcemaps.write("./", {
            sourceRoot: "./", sourceMappingURL: function (file)
            {
                return "/" + dirPath + "/" + file.sourceMap.file + ".map";
            }
        }))
        .pipe(gulp.dest(dest));

    stream.on('end', function () {
        var timePassed = Date.now() - startMilliseconds;
        log("Finished compiling " + chalk.green(filePath) + " - took " + timePassed / 1000 + " seconds");
        currentCompileCount--;
    });

    return stream;
}

async function start(build, shallWatch, run) {
    if (build)
        deleteAllFilesInDest();

    buildItemsInFolder("app", build, shallWatch);
    buildItemsInFolder("layouts", build, shallWatch);
    buildItemsInFolder("pages", build, shallWatch);
    buildItemsInFolder("webobjects", build, shallWatch);
    buildItemsInFolder("formula", build, shallWatch);

    while (currentCompileCount !== 0) {
        await timeout(100);
    }

    if (run)
        startBrowserSync();
}
function buildItemsInFolder(folderName, build, shallWatch) {
    if (shallWatch) {
        watch([`${folderName}/**/*.ts`, `${folderName}/**/*.js`], function (vinyl) {
            var path = transformAbsolutePathToRelativePath(vinyl.path);
            if (vinyl.event === "change" || vinyl.event === "add")
                compileTypescript(path);
            else if (vinyl.event === "unlink") {
                deleteFileInWWWRoot(renameToFileType(path, "js"));
                deleteFileInWWWRoot(renameToFileType(path, "js") + ".map");
            }
        });

        watch([`${folderName}/**/*.scss`, `${folderName}/**/*.css`], function (vinyl) {
            var path = transformAbsolutePathToRelativePath(vinyl.path);
            if (vinyl.event === "change" || vinyl.event === "add")
                compileSass(path);
            else if (vinyl.event === "unlink") {
                deleteFileInWWWRoot(renameToFileType(path, "css"));
                deleteFileInWWWRoot(renameToFileType(path, "css") + ".map");
            }
        });
    }

    if (build === true) {
        compileTypescripts(folderName);
        compileSasses(folderName);
        var appDirs = getDirsAndSubDirs(folderName);
        for (var i = 0; i < appDirs.length; i++) {
            compileTypescripts(appDirs[i]);
            compileSasses(appDirs[i]);
        }
    }
}
function startBrowserSync() {
    browserSync.init({
        open: "external",
        host: proxyUrl,
        proxy: "https://" + proxyUrl,
        files: [
            {
                match: ['wwwroot/**/*.css', 'wwwroot/**/*.js',
                    'App/**/*.cshtml',
                    'Layouts/**/*.cshtml',
                    'Pages/**/*.cshtml',
                    'WebObjects/**/*.cshtml'],
                fn: function (event, file) {
                    this.reload();
                }
            }
        ]
    });
}
