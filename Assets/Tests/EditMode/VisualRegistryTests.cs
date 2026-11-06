using LordsLegacy.EditorTools.Visuals;
using LordsLegacy.Game.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.EditMode
{
    public class VisualRegistryTests
    {
        VisualRegistry _registry;
        VisualDefinition _tree;
        VisualDefinition _rock;

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            _tree = ScriptableObject.CreateInstance<VisualDefinition>();
            _tree.Configure("visual.tree.generic", Resources.GetBuiltinResource<Mesh>("Capsule.fbx"), new Material[0], 1f, true);
            _rock = ScriptableObject.CreateInstance<VisualDefinition>();
            _rock.Configure("visual.rock.final", Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 2f, false);
            _registry.Register(_tree);
            _registry.Register(_rock);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tree);
            Object.DestroyImmediate(_rock);
            Object.DestroyImmediate(_registry);
        }

        [Test]
        public void LookupByIdReturnsRegisteredDefinition()
        {
            Assert.IsTrue(_registry.TryGet("visual.tree.generic", out var found));
            Assert.AreSame(_tree, found);
            Assert.AreSame(_rock, _registry.Get("visual.rock.final"));
            Assert.AreEqual(2f, _rock.Scale, 0.0001f);
            Assert.IsFalse(_rock.IsPlaceholder);
        }

        [Test]
        public void UnknownIdFallsBackAndLogsError()
        {
            Assert.IsFalse(_registry.TryGet("visual.does.not.exist", out _));
            LogAssert.Expect(LogType.Error, "VisualRegistry: unknown visual id 'visual.does.not.exist'. Using the fallback placeholder.");
            var fallback = _registry.Get("visual.does.not.exist");
            Assert.IsNotNull(fallback);
            Assert.AreEqual(VisualRegistry.FallbackId, fallback.Id);
            Assert.IsTrue(fallback.IsPlaceholder);
            Assert.IsNotNull(fallback.Mesh, "fallback must have a drawable mesh");
            Assert.AreSame(fallback, _registry.Fallback, "fallback is created once per registry");
        }

        [Test]
        public void DuplicateIdIsRejected()
        {
            var dup = ScriptableObject.CreateInstance<VisualDefinition>();
            dup.Configure("visual.tree.generic", null, new Material[0], 1f, true);
            LogAssert.Expect(LogType.Error, "VisualRegistry.Register: duplicate id 'visual.tree.generic'.");
            _registry.Register(dup);
            Assert.AreEqual(2, _registry.Definitions.Count);
            Assert.AreSame(_tree, _registry.Get("visual.tree.generic"));
            Object.DestroyImmediate(dup);
        }

        [Test]
        public void RegistryPlaceholderListingReturnsOnlyPlaceholders()
        {
            var list = _registry.Placeholders();
            Assert.AreEqual(1, list.Count);
            Assert.AreSame(_tree, list[0]);
        }

        [Test]
        public void FindPlaceholdersSkipsNullsAndFinalArt()
        {
            var list = PlaceholderVisualLister.FindPlaceholders(new[] { _rock, null, _tree });
            Assert.AreEqual(1, list.Count);
            Assert.AreSame(_tree, list[0]);
            Assert.AreEqual(0, PlaceholderVisualLister.FindPlaceholders(null).Count);
        }

        [Test]
        public void CommittedPlaceholderTreeIsInstancedAndMarked()
        {
            var def = AssetDatabase.LoadAssetAtPath<VisualDefinition>(PlaceholderVisualGenerator.TreeDefinitionPath);
            Assert.IsNotNull(def, "run LordsLegacy/Art/Generate placeholder visuals; missing " + PlaceholderVisualGenerator.TreeDefinitionPath);
            Assert.AreEqual("visual.tree.generic", def.Id);
            Assert.IsTrue(def.IsPlaceholder);
            Assert.IsNotNull(def.Mesh);
            Assert.That(def.Materials.Length, Is.GreaterThan(0));
            Assert.IsTrue(def.Materials[0].enableInstancing, "placeholder tree material must enable GPU instancing");

            var registry = AssetDatabase.LoadAssetAtPath<VisualRegistry>(PlaceholderVisualGenerator.RegistryPath);
            Assert.IsNotNull(registry);
            Assert.AreSame(def, registry.Get("visual.tree.generic"));
        }
    }
}
