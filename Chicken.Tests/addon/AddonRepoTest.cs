namespace Chickensoft.Chicken.Tests {
  using System.Collections.Generic;
  using System.IO.Abstractions;
  using System.Threading.Tasks;
  using CliFx.Exceptions;
  using Moq;
  using Shouldly;
  using Xunit;

  public class AddonRepoTest {
    private const string PROJECT_DIR = "some/work/dir";
    private const string CACHE_PATH = ".addons";
    private const string ADDONS_PATH = "addons";
    private readonly Config _config = new(
      ProjectPath: PROJECT_DIR,
      CachePath: PROJECT_DIR + "/" + CACHE_PATH,
      AddonsPath: PROJECT_DIR + "/" + ADDONS_PATH
    );

    private readonly RequiredAddon _addon = new(
      name: "chicken",
      configFilePath: "some/working/dir/addons.json",
      url: "git@github.com:chickensoft-games/Chicken.git",
      checkout: "Main",
      subfolder: "subfolder"
    );

    [Fact]
    public async Task LoadsCacheCorrectlyWhenAddonCacheDoesNotExist() {
      var app = new Mock<IApp>(MockBehavior.Strict);
      var fs = new Mock<IFileSystem>(MockBehavior.Strict);
      app.Setup(app => app.FS).Returns(fs.Object);
      var cacheRepo = new AddonRepo(app.Object);
      var config = new Config(
        ProjectPath: "project/",
        CachePath: "project/.addons",
        AddonsPath: "project/addons"
      );

      var directory = new Mock<IDirectory>(MockBehavior.Strict);
      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists("project/.addons")).Returns(false);
      directory.Setup(dir => dir.CreateDirectory("project/.addons")).Returns(
        new Mock<IDirectoryInfo>().Object
      );
      directory.Setup(dir => dir.GetDirectories("project/.addons")).Returns(
        new string[] {
          "project/.addons/addon_1",
          "project/.addons/addon_2"
        }
      );
      var shell1 = new Mock<IShell>(MockBehavior.Strict);
      var shell2 = new Mock<IShell>(MockBehavior.Strict);
      app.Setup(app => app.CreateShell("project/.addons/addon_1"))
        .Returns(shell1.Object);
      app.Setup(app => app.CreateShell("project/.addons/addon_2"))
        .Returns(shell2.Object);

      var url1 = "git@github.com:chickensoft-games/addon_1.git";
      var url2 = "git@github.com:chickensoft-games/addon_2.git";

      var result1 = new Mock<IProcessResult>(MockBehavior.Strict);
      result1.Setup(r => r.StandardOutput).Returns(url1);

      var result2 = new Mock<IProcessResult>(MockBehavior.Strict);
      result2.Setup(r => r.StandardOutput).Returns(url2);

      shell1.Setup(sh => sh.Run("git", "remote", "get-url", "origin"))
        .Returns(Task.FromResult(result1.Object));

      shell2.Setup(sh => sh.Run("git", "remote", "get-url", "origin"))
        .Returns(Task.FromResult(result2.Object));

      var urls = await cacheRepo.LoadCache(config);
      urls.ShouldBe(new Dictionary<string, string> {
        { url1, "project/.addons/addon_1" },
        { url2, "project/.addons/addon_2" }
      });
    }

    [Fact]
    public async void CacheAddonCachesAddon() {
      var addonCachePath = $"{_config.CachePath}/{_addon.Name}";

      var app = new Mock<IApp>();
      var fs = new Mock<IFileSystem>();
      var directory = new Mock<IDirectory>();

      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists(addonCachePath)).Returns(false);

      var cli = new ShellVerifier();

      var cachePathShell = cli.CreateShell(addonCachePath);

      app.Setup(app => app.FS).Returns(fs.Object);
      app.Setup(app => app.CreateShell(_config.CachePath))
        .Returns(cachePathShell.Object);

      cli.Setup(
        addonCachePath,
        new ProcessResult(0),
        RunMode.Run,
        "git", "clone", _addon.Url, "--recurse-submodules", _addon.Name
      );

      var addonRepo = new AddonRepo(app.Object);

      await addonRepo.CacheAddon(_addon, _config);

      directory.VerifyAll();
      cli.VerifyAll();
    }

    [Fact]
    public async Task CacheAddonDoesNothingIfAddonIsAlreadyCached() {
      var addonCachePath = $"{_config.CachePath}/{_addon.Name}";

      var app = new Mock<IApp>(MockBehavior.Strict);
      var fs = new Mock<IFileSystem>(MockBehavior.Strict);
      var directory = new Mock<IDirectory>(MockBehavior.Strict);

      app.Setup(app => app.FS).Returns(fs.Object);
      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists(addonCachePath)).Returns(true);

      var addonRepo = new AddonRepo(app.Object);

      await addonRepo.CacheAddon(_addon, _config);

      fs.VerifyAll();
      directory.VerifyAll();
      app.VerifyAll();
    }

    [Fact]
    public async Task DeleteAddonDeletesAddon() {
      var addonDir = _config.AddonsPath + "/" + _addon.Name;

      var app = new Mock<IApp>();
      var fs = new Mock<IFileSystem>();
      var directory = new Mock<IDirectory>();

      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists(addonDir)).Returns(true);

      var cli = new ShellVerifier();

      var addonShell = cli.CreateShell(addonDir);
      var addonsPathShell = cli.CreateShell(_config.AddonsPath);

      app.Setup(app => app.FS).Returns(fs.Object);
      app.Setup(app => app.CreateShell(addonDir))
        .Returns(addonShell.Object);
      app.Setup(app => app.CreateShell(_config.AddonsPath))
        .Returns(addonsPathShell.Object);

      cli.Setup(
        addonDir,
        new ProcessResult(0),
        RunMode.RunUnchecked,
        "git", "status", "--porcelain"
      );

      cli.Setup(
        _config.AddonsPath,
        new ProcessResult(0),
        RunMode.Run,
        "rm", "-rf", $"{_config.AddonsPath}/{_addon.Name}"
      );

      var addonRepo = new AddonRepo(app.Object);

      await addonRepo.DeleteAddon(_addon, _config);

      directory.VerifyAll();
      cli.VerifyAll();
    }

    [Fact]
    public async Task DeleteAddonDoesNothingIfAddonDoesNotExist() {
      var addonDir = _config.AddonsPath + "/" + _addon.Name;

      var app = new Mock<IApp>(MockBehavior.Strict);
      var fs = new Mock<IFileSystem>(MockBehavior.Strict);
      var directory = new Mock<IDirectory>(MockBehavior.Strict);

      app.Setup(app => app.FS).Returns(fs.Object);
      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists(addonDir)).Returns(false);

      var addonRepo = new AddonRepo(app.Object);

      await addonRepo.DeleteAddon(_addon, _config);

      fs.VerifyAll();
      directory.VerifyAll();
      app.VerifyAll();
    }

    [Fact]
    public async Task DeleteAddonThrowsIfAddonModified() {
      var addonDir = _config.AddonsPath + "/" + _addon.Name;

      var app = new Mock<IApp>();
      var fs = new Mock<IFileSystem>();
      var directory = new Mock<IDirectory>();

      fs.Setup(fs => fs.Directory).Returns(directory.Object);
      directory.Setup(dir => dir.Exists(addonDir)).Returns(true);

      var cli = new ShellVerifier();

      var addonShell = cli.CreateShell(addonDir);
      var addonsPathShell = cli.CreateShell(_config.AddonsPath);

      app.Setup(app => app.FS).Returns(fs.Object);
      app.Setup(app => app.CreateShell(addonDir))
        .Returns(addonShell.Object);
      app.Setup(app => app.CreateShell(_config.AddonsPath))
        .Returns(addonsPathShell.Object);

      cli.Setup(
        addonDir,
        new ProcessResult(0, StandardOutput: "a file was changed"),
        RunMode.RunUnchecked,
        "git", "status", "--porcelain"
      );

      var addonRepo = new AddonRepo(app.Object);

      await Should.ThrowAsync<CommandException>(
        async ()
          => await addonRepo.DeleteAddon(_addon, _config)
      );

      directory.VerifyAll();
      cli.VerifyAll();
    }

    [Fact]
    public async Task CopyAddonCopiesAddon() {
      var addonCachePath = _config.CachePath + "/" + _addon.Name;
      var copyFromPath = addonCachePath + "/" + _addon.Subfolder + "/";
      var workingDir = _config.ProjectPath;
      var addonDir = _config.AddonsPath + "/" + _addon.Name;

      var app = new Mock<IApp>();
      var fs = new Mock<IFileSystem>();
      var cli = new ShellVerifier();
      var addonCacheShell = cli.CreateShell(addonCachePath);
      var workingShell = cli.CreateShell(workingDir);
      var addonShell = cli.CreateShell(addonDir);

      app.Setup(app => app.FS).Returns(fs.Object);
      app.Setup(app => app.CreateShell(addonCachePath))
        .Returns(addonCacheShell.Object);
      app.Setup(app => app.CreateShell(workingDir))
        .Returns(workingShell.Object);
      app.Setup(app => app.CreateShell(addonDir))
        .Returns(addonShell.Object);

      var path = new Mock<IPath>();
      path.Setup(path => path.DirectorySeparatorChar).Returns('/');
      fs.Setup(fs => fs.Path).Returns(path.Object);

      cli.Setup(
        addonCachePath,
        new ProcessResult(0),
        RunMode.Run,
        "git", "checkout", "-f", _addon.Checkout
      );

      cli.Setup(
        addonCachePath,
        new ProcessResult(0),
        RunMode.RunUnchecked,
        "git", "pull"
      );

      cli.Setup(
        addonCachePath,
        new ProcessResult(0),
        RunMode.RunUnchecked,
        "git", "submodule", "update", "--init", "--recursive"
      );

      cli.Setup(
        workingDir,
        new ProcessResult(0),
        RunMode.Run,
        "rsync",
        "-av", copyFromPath, addonDir, "--exclude", ".git"
      );

      cli.Setup(
        addonDir,
        new ProcessResult(0),
        RunMode.Run,
        "git",
        "init"
      );

      cli.Setup(
        addonDir,
        new ProcessResult(0),
        RunMode.Run,
        "git",
        "add", "-A"
      );

      cli.Setup(
        addonDir,
        new ProcessResult(0),
        RunMode.Run,
        "git",
        "commit", "-m", "Initial commit"
      );

      var addonRepo = new AddonRepo(app.Object);
      await addonRepo.CopyAddonFromCache(_addon, _config);

      app.VerifyAll();
      cli.VerifyAll();
    }
  }
}
