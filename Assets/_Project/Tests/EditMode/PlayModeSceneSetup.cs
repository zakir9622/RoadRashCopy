using UnityEngine.TestTools;
using HighwayRenegade.Editor;

namespace HighwayRenegade.Tests.EditMode
{
    /// <summary>
    /// Makes sure every scene exists before PlayMode tests try to load one.
    ///
    /// Only TestTrack.unity is committed; MainMenu and Garage are generated. The test job
    /// runs before the build job, so without this a PlayMode test that loads MainMenu by
    /// name fails on a scene that has never been written - and the boot smoke test walks
    /// menu to race to garage and back, which is exactly that.
    ///
    /// EditorSceneBootstrap also fills the gap on editor load, but it defers by a tick to
    /// stay clear of the domain reload, and a deferred callback racing the test runner is
    /// not a guarantee. IPrebuildSetup is ordered: it runs before the assembly's tests,
    /// every time.
    ///
    /// Lives in the EditMode assembly because that one is Editor-only and already
    /// references HighwayRenegade.Editor. PlayMode tests reach it by name through
    /// PrebuildSetup's string overload, which needs no assembly reference and therefore
    /// does not drag editor code into a player build.
    /// </summary>
    public sealed class PlayModeSceneSetup : IPrebuildSetup
    {
        public void Setup() => EditorSceneBootstrap.EnsureMissingScenes();
    }
}
