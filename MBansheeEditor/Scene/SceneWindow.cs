﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BansheeEngine;

namespace BansheeEditor
{
    internal sealed class SceneWindow : EditorWindow
    {
        internal const string ToggleProfilerOverlayBinding = "ToggleProfilerOverlay";
        internal const string ViewToolBinding = "EdViewTool";
        internal const string MoveToolBinding = "EdMoveTool";
        internal const string RotateToolBinding = "EdRotateTool";
        internal const string ScaleToolBinding = "EdScaleTool";
        internal const string DuplicateBinding = "EdDuplicate";

        private const int HeaderHeight = 20;
        private const float DefaultPlacementDepth = 5.0f;
        private static readonly Color ClearColor = new Color(83.0f/255.0f, 83.0f/255.0f, 83.0f/255.0f);
        private const string ProfilerOverlayActiveKey = "_Internal_ProfilerOverlayActive";

        private Camera camera;
        private SceneCamera cameraController;
        private RenderTexture2D renderTexture;
        private GUILayoutY mainLayout;

        private GUIRenderTexture renderTextureGUI;
        private SceneViewHandler sceneViewHandler;

        private GUIToggle viewButton;
        private GUIToggle moveButton;
        private GUIToggle rotateButton;
        private GUIToggle scaleButton;

        private GUIToggle localCoordButton;
        private GUIToggle worldCoordButton;

        private GUIToggle pivotButton;
        private GUIToggle centerButton;

        private GUIToggle moveSnapButton;
        private GUIFloatField moveSnapInput;

        private GUIToggle rotateSnapButton;
        private GUIFloatField rotateSnapInput;

        private int editorSettingsHash = int.MaxValue;

        private VirtualButton duplicateKey;

        // Tool shortcuts
        private VirtualButton viewToolKey;
        private VirtualButton moveToolKey;
        private VirtualButton rotateToolKey;
        private VirtualButton scaleToolKey;

        // Profiler overlay
        private ProfilerOverlay activeProfilerOverlay;
        private Camera profilerCamera;
        private VirtualButton toggleProfilerOverlayKey;

        // Drag & drop
        private bool dragActive;
        private SceneObject draggedSO;

        public Camera GetCamera()
        {
            return camera;
        }

        internal SceneWindow()
        { }

        [MenuItem("Windows/Scene", ButtonModifier.CtrlAlt, ButtonCode.S)]
        private static void OpenSceneWindow()
        {
            OpenWindow<SceneWindow>();
        }

        protected override LocString GetDisplayName()
        {
            return new LocEdString("Scene");
        }

        private void OnInitialize()
        {
            mainLayout = GUI.AddLayoutY();

            GUIToggleGroup handlesTG = new GUIToggleGroup();
            viewButton = new GUIToggle("V", handlesTG, EditorStyles.Button);
            moveButton = new GUIToggle("M", handlesTG, EditorStyles.Button);
            rotateButton = new GUIToggle("R", handlesTG, EditorStyles.Button);
            scaleButton = new GUIToggle("S", handlesTG, EditorStyles.Button);

            GUIToggleGroup coordModeTG = new GUIToggleGroup();
            localCoordButton = new GUIToggle("L", coordModeTG, EditorStyles.Button);
            worldCoordButton = new GUIToggle("W", coordModeTG, EditorStyles.Button);

            GUIToggleGroup pivotModeTG = new GUIToggleGroup();
            pivotButton = new GUIToggle("P", pivotModeTG, EditorStyles.Button);
            centerButton = new GUIToggle("C", pivotModeTG, EditorStyles.Button);

            moveSnapButton = new GUIToggle("MS", EditorStyles.Button);
            moveSnapInput = new GUIFloatField();

            rotateSnapButton = new GUIToggle("RS", EditorStyles.Button);
            rotateSnapInput = new GUIFloatField();

            viewButton.OnClick += () => OnSceneToolButtonClicked(SceneViewTool.View);
            moveButton.OnClick += () => OnSceneToolButtonClicked(SceneViewTool.Move);
            rotateButton.OnClick += () => OnSceneToolButtonClicked(SceneViewTool.Rotate);
            scaleButton.OnClick += () => OnSceneToolButtonClicked(SceneViewTool.Scale);

            localCoordButton.OnClick += () => OnCoordinateModeButtonClicked(HandleCoordinateMode.Local);
            worldCoordButton.OnClick += () => OnCoordinateModeButtonClicked(HandleCoordinateMode.World);

            pivotButton.OnClick += () => OnPivotModeButtonClicked(HandlePivotMode.Pivot);
            centerButton.OnClick += () => OnPivotModeButtonClicked(HandlePivotMode.Center);

            moveSnapButton.OnToggled += (bool active) => OnMoveSnapToggled(active);
            moveSnapInput.OnChanged += (float value) => OnMoveSnapValueChanged(value);

            rotateSnapButton.OnToggled += (bool active) => OnRotateSnapToggled(active);
            rotateSnapInput.OnChanged += (float value) => OnRotateSnapValueChanged(value);

            GUILayout handlesLayout = mainLayout.AddLayoutX();
            handlesLayout.AddElement(viewButton);
            handlesLayout.AddElement(moveButton);
            handlesLayout.AddElement(rotateButton);
            handlesLayout.AddElement(scaleButton);
            handlesLayout.AddSpace(10);
            handlesLayout.AddElement(localCoordButton);
            handlesLayout.AddElement(worldCoordButton);
            handlesLayout.AddSpace(10);
            handlesLayout.AddElement(pivotButton);
            handlesLayout.AddElement(centerButton);
            handlesLayout.AddFlexibleSpace();
            handlesLayout.AddElement(moveSnapButton);
            handlesLayout.AddElement(moveSnapInput);
            handlesLayout.AddSpace(10);
            handlesLayout.AddElement(rotateSnapButton);
            handlesLayout.AddElement(rotateSnapInput);

            toggleProfilerOverlayKey = new VirtualButton(ToggleProfilerOverlayBinding);
            viewToolKey = new VirtualButton(ViewToolBinding);
            moveToolKey = new VirtualButton(MoveToolBinding);
            rotateToolKey = new VirtualButton(RotateToolBinding);
            scaleToolKey = new VirtualButton(ScaleToolBinding);
            duplicateKey = new VirtualButton(DuplicateBinding);

            UpdateRenderTexture(Width, Height - HeaderHeight);
            UpdateProfilerOverlay();
        }

        private void OnDestroy()
        {
            if (camera != null)
            {
                camera.SceneObject.Destroy();
                camera = null;
            }
        }

        private bool ScreenToScenePos(Vector2I screenPos, out Vector2I scenePos)
        {
            scenePos = screenPos;
            Vector2I windowPos = ScreenToWindowPos(screenPos);

            Rect2I bounds = GUILayoutUtility.CalculateBounds(renderTextureGUI);

            if (bounds.Contains(windowPos))
            {
                scenePos.x = windowPos.x - bounds.x;
                scenePos.y = windowPos.y - bounds.y;

                return true;
            }

            return false;
        }

        private void OnEditorUpdate()
        {
            if (HasFocus)
            {
                if (VirtualInput.IsButtonUp(toggleProfilerOverlayKey))
                    EditorSettings.SetBool(ProfilerOverlayActiveKey, !EditorSettings.GetBool(ProfilerOverlayActiveKey));

                if(VirtualInput.IsButtonUp(viewToolKey))
                    EditorApplication.ActiveSceneTool = SceneViewTool.View;

                if (VirtualInput.IsButtonUp(moveToolKey))
                    EditorApplication.ActiveSceneTool = SceneViewTool.Move;

                if(VirtualInput.IsButtonUp(rotateToolKey))
                    EditorApplication.ActiveSceneTool = SceneViewTool.Rotate;

                if (VirtualInput.IsButtonUp(scaleToolKey))
                    EditorApplication.ActiveSceneTool = SceneViewTool.Scale;

                if (VirtualInput.IsButtonUp(duplicateKey))
                {
                    SceneObject[] selectedObjects = Selection.sceneObjects;
                    CleanDuplicates(ref selectedObjects);

                    if (selectedObjects.Length > 0)
                    {
                        String message;
		                if (selectedObjects.Length == 1)
			                message = "Duplicated " + selectedObjects[0].Name;
		                else
			                message = "Duplicated " + selectedObjects.Length + " elements";

                        UndoRedo.CloneSO(selectedObjects, message);
                    }
                }
            }

            // Refresh GUI buttons if needed (in case someones changes the values from script)
            if (editorSettingsHash != EditorSettings.Hash)
            {
                UpdateButtonStates();
                UpdateProfilerOverlay();
                editorSettingsHash = EditorSettings.Hash;
            }

            // Update scene view handles and selection
            sceneViewHandler.Update();

            bool handleActive = false;
            if (Input.IsPointerButtonUp(PointerButton.Left))
            {
                if (sceneViewHandler.IsHandleActive())
                {
                    sceneViewHandler.ClearHandleSelection();
                    handleActive = true;
                }
            }

            Vector2I scenePos;
            bool inBounds = ScreenToScenePos(Input.PointerPosition, out scenePos);

            bool draggedOver = DragDrop.DragInProgress || DragDrop.DropInProgress;
            draggedOver &= inBounds && DragDrop.Type == DragDropType.Resource;

            if (draggedOver)
            {
                if (DragDrop.DropInProgress)
                {
                    dragActive = false;
                    draggedSO = null;
                }
                else
                {
                    if (!dragActive)
                    {
                        dragActive = true;

                        ResourceDragDropData dragData = (ResourceDragDropData)DragDrop.Data;

                        string draggedMeshPath = "";
                        string[] draggedPaths = dragData.Paths;

                        for (int i = 0; i < draggedPaths.Length; i++)
                        {
                            LibraryEntry entry = ProjectLibrary.GetEntry(draggedPaths[i]);
                            if (entry != null && entry.Type == LibraryEntryType.File)
                            {
                                FileEntry fileEntry = (FileEntry) entry;
                                if (fileEntry.ResType == ResourceType.Mesh)
                                {
                                    draggedMeshPath = draggedPaths[i];
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(draggedMeshPath))
                        {
                            string meshName = Path.GetFileName(draggedMeshPath);

                            draggedSO = new SceneObject(meshName);
                            Mesh mesh = ProjectLibrary.Load<Mesh>(draggedMeshPath);
                            Material material = new Material(Builtin.DiffuseShader);

                            Renderable renderable = draggedSO.AddComponent<Renderable>();
                            renderable.Mesh = mesh;
                            renderable.SetMaterial(material);
                        }
                    }

                    if (draggedSO != null)
                    {
                        Ray worldRay = camera.ScreenToWorldRay(scenePos);
                        draggedSO.Position = worldRay*DefaultPlacementDepth;
                    }
                }

                return;
            }
            else
            {
                if (dragActive)
                {
                    dragActive = false;

                    if (draggedSO != null)
                    {
                        draggedSO.Destroy();
                        draggedSO = null;
                    }
                }
            }

            if (HasFocus)
            {
                cameraController.SceneObject.Active = true;

                if (inBounds)
                {
                    if (Input.IsPointerButtonDown(PointerButton.Left))
                    {
                        sceneViewHandler.TrySelectHandle(scenePos);
                    }
                    else if (Input.IsPointerButtonUp(PointerButton.Left))
                    {
                        if (!handleActive)
                        {
                            bool ctrlHeld = Input.IsButtonHeld(ButtonCode.LeftControl) ||
                                            Input.IsButtonHeld(ButtonCode.RightControl);

                            sceneViewHandler.PickObject(scenePos, ctrlHeld);
                        }
                    }
                }
            }
            else
                cameraController.SceneObject.Active = false;

            sceneViewHandler.UpdateHandle(scenePos, Input.PointerDelta);
            sceneViewHandler.UpdateSelection();
        }

        protected override void WindowResized(int width, int height)
        {
            UpdateRenderTexture(width, height - HeaderHeight);

            base.WindowResized(width, height);
        }

        protected override void FocusChanged(bool inFocus)
        {
            if (!inFocus)
            {
                sceneViewHandler.ClearHandleSelection();
            }
        }

        private void OnSceneToolButtonClicked(SceneViewTool tool)
        {
            EditorApplication.ActiveSceneTool = tool;
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnCoordinateModeButtonClicked(HandleCoordinateMode mode)
        {
            EditorApplication.ActiveCoordinateMode = mode;
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnPivotModeButtonClicked(HandlePivotMode mode)
        {
            EditorApplication.ActivePivotMode = mode;
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnMoveSnapToggled(bool active)
        {
            Handles.MoveHandleSnapActive = active;
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnMoveSnapValueChanged(float value)
        {
            Handles.MoveSnapAmount = MathEx.Clamp(value, 0.01f, 1000.0f);
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnRotateSnapToggled(bool active)
        {
            Handles.RotateHandleSnapActive = active;
            editorSettingsHash = EditorSettings.Hash;
        }

        private void OnRotateSnapValueChanged(float value)
        {
            Handles.RotateSnapAmount = MathEx.Clamp(value, 0.01f, 360.0f);
            editorSettingsHash = EditorSettings.Hash;
        }

        private void UpdateButtonStates()
        {
            switch (EditorApplication.ActiveSceneTool)
            {
                case SceneViewTool.View:
                    viewButton.ToggleOn();
                    break;
                case SceneViewTool.Move:
                    moveButton.ToggleOn();
                    break;
                case SceneViewTool.Rotate:
                    rotateButton.ToggleOn();
                    break;
                case SceneViewTool.Scale:
                    scaleButton.ToggleOn();
                    break;
            }

            switch (EditorApplication.ActiveCoordinateMode)
            {
                case HandleCoordinateMode.Local:
                    localCoordButton.ToggleOn();
                    break;
                case HandleCoordinateMode.World:
                    worldCoordButton.ToggleOn();
                    break;
            }

            switch (EditorApplication.ActivePivotMode)
            {
                case HandlePivotMode.Center:
                    centerButton.ToggleOn();
                    break;
                case HandlePivotMode.Pivot:
                    pivotButton.ToggleOn();
                    break;
            }

            if (Handles.MoveHandleSnapActive)
                moveSnapButton.ToggleOn();
            else
                moveSnapButton.ToggleOff();

            moveSnapInput.Value = Handles.MoveSnapAmount;

            if (Handles.RotateHandleSnapActive)
                rotateSnapButton.ToggleOn();
            else
                rotateSnapButton.ToggleOff();

            moveSnapInput.Value = Handles.RotateSnapAmount.Degrees;
        }

        private void UpdateProfilerOverlay()
        {
            if (EditorSettings.GetBool(ProfilerOverlayActiveKey))
            {
                if (activeProfilerOverlay == null)
                {
                    SceneObject profilerSO = new SceneObject("EditorProfilerOverlay");
                    profilerCamera = profilerSO.AddComponent<Camera>();
                    profilerCamera.Target = renderTexture;
                    profilerCamera.ClearFlags = ClearFlags.None;
                    profilerCamera.Priority = 1;
                    profilerCamera.Layers = 0;

                    activeProfilerOverlay = profilerSO.AddComponent<ProfilerOverlay>();
                }
            }
            else
            {
                if (activeProfilerOverlay != null)
                {
                    activeProfilerOverlay.SceneObject.Destroy();
                    activeProfilerOverlay = null;
                    profilerCamera = null;
                }
            }
        }

        private void UpdateRenderTexture(int width, int height)
	    {
            width = MathEx.Max(20, width);
            height = MathEx.Max(20, height);

            renderTexture = new RenderTexture2D(PixelFormat.R8G8B8A8, width, height);
            renderTexture.Priority = 1;

		    if (camera == null)
		    {
                SceneObject sceneCameraSO = new SceneObject("SceneCamera", true);
                camera = sceneCameraSO.AddComponent<Camera>();
                camera.Target = renderTexture;
                camera.ViewportRect = new Rect2(0.0f, 0.0f, 1.0f, 1.0f);

                sceneCameraSO.Position = new Vector3(0, 0.5f, 1);
                sceneCameraSO.LookAt(new Vector3(0, 0, 0));

                camera.Priority = 2;
                camera.NearClipPlane = 0.005f;
                camera.FarClipPlane = 1000.0f;
                camera.ClearColor = ClearColor;

                cameraController = sceneCameraSO.AddComponent<SceneCamera>();

                renderTextureGUI = new GUIRenderTexture(renderTexture);
                mainLayout.AddElement(renderTextureGUI);

                sceneViewHandler = new SceneViewHandler(this, camera);
		    }
		    else
		    {
		        camera.Target = renderTexture;
		        renderTextureGUI.RenderTexture = renderTexture;
		    }

		    // TODO - Consider only doing the resize once user stops resizing the widget in order to reduce constant
		    // render target destroy/create cycle for every single pixel.

		    camera.AspectRatio = width / (float)height;

            if (profilerCamera != null)
                profilerCamera.Target = renderTexture;
	    }

        private void CleanDuplicates(ref SceneObject[] objects)
	    {
		    List<SceneObject> cleanList = new List<SceneObject>();
		    for (int i = 0; i < objects.Length; i++)
		    {
			    bool foundParent = false;
                for (int j = 0; j < objects.Length; j++)
                {
                    SceneObject elem = objects[i];

                    while (elem != null && elem != objects[j])
                        elem = objects[i].Parent;

                    bool isChildOf =  elem == objects[j];

				    if (i != j && isChildOf)
				    {
					    foundParent = true;
					    break;
				    }
			    }

			    if (!foundParent)
				    cleanList.Add(objects[i]);
		    }

		    objects = cleanList.ToArray();
	    }
    }
}
