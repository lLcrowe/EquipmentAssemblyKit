using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace lLCroweTool.EquipmentAssemblyKit.Tests
{
    public class EquipmentTreeModifierTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void RegisteredEquipment_AppliesRootAndChildExactlyOnce()
        {
            var controller = CreateController();
            float appliedValue = 0f;
            controller.ApplyStat = (_, _, value) => appliedValue += value;
            controller.RemoveStat = (_, _, value) => appliedValue -= value;

            var equipment = CreateEquipment();
            var rootInfo = CreatePart("root", 10f, "child");
            var childInfo = CreatePart("child", 2f);

            Assert.That(controller.AddPart(equipment, 0, rootInfo), Is.True);
            Assert.That(appliedValue, Is.Zero, "조립만 한 미등록 장비는 실스탯에 반영하면 안 된다.");

            controller.RegisterEquipment(equipment);
            Assert.That(appliedValue, Is.EqualTo(10f));

            var root = equipment.parts[0];
            Assert.That(controller.AddChildPart(equipment, root, 0, childInfo), Is.True);
            Assert.That(appliedValue, Is.EqualTo(12f));

            controller.RegisterEquipment(equipment);
            Assert.That(appliedValue, Is.EqualTo(12f), "중복 등록은 스탯을 중첩하면 안 된다.");

            Assert.That(controller.RemoveChildPart(equipment, root, 0), Is.Not.Null);
            Assert.That(appliedValue, Is.EqualTo(10f));

            controller.UnregisterEquipment(equipment);
            Assert.That(appliedValue, Is.Zero);
        }

        [Test]
        public void PartBuffCallbacks_RemainPerPart()
        {
            var controller = CreateController();
            int appliedBuffCount = 0;
            int removedBuffCount = 0;
            controller.BindBuffCallbacks<PartInfo>(
                _ => appliedBuffCount++,
                _ => removedBuffCount++);

            var equipment = CreateEquipment();
            var buffMarker = Create<PartInfo>();
            var rootInfo = CreatePart("root", 10f);
            rootInfo.contribution.partBuffs = new ScriptableObject[] { buffMarker };

            Assert.That(controller.AddPart(equipment, 0, rootInfo), Is.True);
            Assert.That(appliedBuffCount, Is.EqualTo(1));

            Assert.That(controller.RemovePart(equipment, 0), Is.Not.Null);
            Assert.That(removedBuffCount, Is.EqualTo(1));
        }

        private ExampleEquipmentController CreateController()
        {
            var gameObject = new GameObject("EquipmentTreeModifierTests");
            createdObjects.Add(gameObject);
            return gameObject.AddComponent<ExampleEquipmentController>();
        }

        private AssembledEquipment CreateEquipment()
        {
            var equipmentInfo = Create<EquipmentInfo>();
            equipmentInfo.rootSlots = new[] { CreateSlot("root") };
            return new AssembledEquipment(equipmentInfo);
        }

        private PartInfo CreatePart(string tag, float modifierValue, string childTag = null)
        {
            var part = Create<PartInfo>();
            part.partTags = new[] { tag };
            part.childSlots = childTag == null
                ? new PartSlot[0]
                : new[] { CreateSlot(childTag) };
            part.contribution = new PartContribution
            {
                statModifiers = new[]
                {
                    new EquipmentStatModifier
                    {
                        statType = "STAT_TEST",
                        modifierType = ModifierType.Flat,
                        value = modifierValue
                    }
                }
            };
            return part;
        }

        private static PartSlot CreateSlot(string acceptedTag)
        {
            return new PartSlot
            {
                acceptedTags = new[] { acceptedTag }
            };
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }
    }
}
