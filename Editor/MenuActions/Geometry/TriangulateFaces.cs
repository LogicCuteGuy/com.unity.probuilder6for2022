using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace UnityEditor.ProBuilder.Actions
{
    sealed class TriangulateFaces : MenuAction
    {
        readonly Pref<bool> m_FlipCut = new Pref<bool>("TriangulateFaces.flipCut", false);

        public override ToolbarGroup group
        {
            get { return ToolbarGroup.Geometry; }
        }

        public override string iconPath => "Toolbar/Face_Triangulate";
        public override Texture2D icon => IconUtility.GetIcon(iconPath);

        public override TooltipContent tooltip
        {
            get { return s_Tooltip; }
        }

        protected internal override bool hasFileMenuEntry
        {
            get { return false; }
        }

        static readonly TooltipContent s_Tooltip = new TooltipContent
            (
                "Triangulate Faces",
                "Break all selected faces down to triangles."
            );

        public override SelectMode validSelectModes
        {
            get { return SelectMode.Face; }
        }

        public override bool enabled
        {
            get { return base.enabled && MeshSelection.selectedFaceCount > 0; }
        }

        protected override MenuActionState optionsMenuState
        {
            get { return MenuActionState.VisibleAndEnabled; }
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();
            var flipCutField = new Toggle("Flip Cut") { value = m_FlipCut.value };
            flipCutField.tooltip = "When enabled, quads flip their diagonal before triangulation.";
            flipCutField.RegisterValueChangedCallback(OnFlipCutChanged);
            root.Add(flipCutField);
            return root;
        }

        void OnFlipCutChanged(ChangeEvent<bool> evt)
        {
            if (m_FlipCut.value == evt.newValue)
                return;

            m_FlipCut.SetValue(evt.newValue);
            PreviewActionManager.UpdatePreview();
        }

        protected override void OnSettingsGUI()
        {
            GUILayout.Label("Triangulate Face Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            m_FlipCut.value = EditorGUILayout.Toggle("Flip Cut", m_FlipCut.value);

            if (EditorGUI.EndChangeCheck())
                ProBuilderSettings.Save();

            EditorGUILayout.HelpBox("Flip Cut switches the quad diagonal before triangulating selected faces.", MessageType.Info);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Triangulate Faces"))
                PerformAction();
        }

        protected override ActionResult PerformActionImplementation()
        {
            ActionResult res = ActionResult.NoSelection;

            UndoUtility.RecordSelection("Triangulate Faces");

            foreach (ProBuilderMesh mesh in MeshSelection.topInternal)
            {
                mesh.ToMesh();

                if (m_FlipCut.value)
                {
                    var selected = mesh.selectedFacesInternal;
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (selected[i].IsQuad())
                            mesh.FlipEdge(selected[i]);
                    }
                }

                Face[] triangulatedFaces = mesh.ToTriangles(mesh.selectedFacesInternal);
                mesh.Refresh();
                mesh.Optimize();
                mesh.SetSelectedFaces(triangulatedFaces);
                res = new ActionResult(ActionResult.Status.Success, string.Format("Triangulated {0} {1}", triangulatedFaces.Length, triangulatedFaces.Length < 2 ? "Face" : "Faces"));
            }

            ProBuilderEditor.Refresh();

            return res;
        }
    }
}
