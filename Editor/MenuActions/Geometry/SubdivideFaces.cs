using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace UnityEditor.ProBuilder.Actions
{
    sealed class SubdivideFaces : MenuAction
    {
        readonly Pref<int> m_SubdivisionAxis = new Pref<int>("SubdivideFaces.axis", (int)SubdivisionAxis.XY);

        public override ToolbarGroup group
        {
            get { return ToolbarGroup.Geometry; }
        }

        public override string iconPath => "Toolbar/Face_Subdivide";
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
                "Subdivide Faces",
                @"Inserts a new vertex at the center of each selected face and creates a new edge from the center of each perimeter edge to the center vertex.",
                keyCommandAlt, 'S'
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
            var axisField = new EnumField("Direction", (SubdivisionAxis)m_SubdivisionAxis.value);
            axisField.tooltip = "Choose subdivision direction: X (left to right), Y (top to bottom), or XY (both directions).";
            axisField.RegisterValueChangedCallback(OnSubdivisionAxisChanged);
            root.Add(axisField);
            return root;
        }

        void OnSubdivisionAxisChanged(ChangeEvent<System.Enum> evt)
        {
            var axis = (SubdivisionAxis)evt.newValue;

            if (m_SubdivisionAxis.value == (int)axis)
                return;

            m_SubdivisionAxis.SetValue((int)axis);
            PreviewActionManager.UpdatePreview();
        }

        protected override void OnSettingsGUI()
        {
            GUILayout.Label("Subdivide Face Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var axis = (SubdivisionAxis)m_SubdivisionAxis.value;
            axis = (SubdivisionAxis)EditorGUILayout.EnumPopup("Direction", axis);

            if (EditorGUI.EndChangeCheck())
            {
                m_SubdivisionAxis.value = (int)axis;
                ProBuilderSettings.Save();
            }

            EditorGUILayout.HelpBox("X cuts left to right, Y cuts top to bottom, and XY applies both directions.", MessageType.Info);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Subdivide Faces"))
                PerformAction();
        }

        protected override ActionResult PerformActionImplementation()
        {
            if (MeshSelection.selectedObjectCount < 1)
                return ActionResult.NoSelection;

            int success = 0;
            var axis = (SubdivisionAxis)m_SubdivisionAxis.value;
            UndoUtility.RecordSelection("Subdivide Faces");

            foreach (ProBuilderMesh pb in MeshSelection.topInternal)
            {
                Face[] faces = pb.Subdivide(pb.selectedFacesInternal, axis);

                pb.ToMesh();

                if (faces != null)
                {
                    success += pb.selectedFacesInternal.Length;
                    pb.SetSelectedFaces(faces);

                    pb.Refresh();
                    pb.Optimize();
                }
            }

            if (success > 0)
            {
                ProBuilderEditor.Refresh();

                return new ActionResult(ActionResult.Status.Success, "Subdivide " + success + ((success > 1) ? " faces" : " face"));
            }
            else
            {
                Debug.LogWarning("Subdivide faces failed - did you not have any faces selected?");
                return new ActionResult(ActionResult.Status.Failure, "Subdivide Faces\nNo faces selected");
            }
        }
    }
}
