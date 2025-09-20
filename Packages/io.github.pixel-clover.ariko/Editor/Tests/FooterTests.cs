using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ariko.Tests
{
    public class FooterTests
    {
        private ArikoWindow window;

        [SetUp]
        public void SetUp()
        {
            window = EditorWindow.GetWindow<ArikoWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            window.Close();
        }

        [Test]
        public void Footer_Contains_Ariko_Version()
        {
            var footerMetadataLabel = window.rootVisualElement.Q<Label>("footer-metadata");
            Assert.IsNotNull(footerMetadataLabel);
            Assert.IsTrue(footerMetadataLabel.text.Contains("Ariko Version:"));
        }
    }
}
