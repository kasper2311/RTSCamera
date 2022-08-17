﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RTSCamera.CommandSystem.Config;
using RTSCamera.CommandSystem.Config.HotKey;
using RTSCamera.CommandSystem.Logic;
using RTSCamera.CommandSystem.Logic.SubLogic;
using RTSCamera.CommandSystem.QuerySystem;
using RTSCamera.CommandSystem.Utilities;
using RTSCamera.Config;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;

namespace RTSCamera.CommandSystem.View
{
    public class CommandSystemOrderTroopPlacer : MissionView
    {
        private FormationColorSubLogic _contourView;
        private readonly CommandSystemConfig _config = CommandSystemConfig.Get();
        private void RegisterReload()
        {
            MissionLibrary.Event.MissionEvent.PostSwitchTeam += OnPostSwitchTeam;
        }
        private void OnPostSwitchTeam()
        {
            InitializeInADisgustingManner();
        }
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            RegisterReload();
            _contourView = Mission.GetMissionBehavior<CommandSystemLogic>().FormationColorSubLogic;
        }

        public override void OnMissionScreenFinalize()
        {
            base.OnMissionScreenFinalize();

            MissionLibrary.Event.MissionEvent.PostSwitchTeam -= OnPostSwitchTeam;
        }

        public Action OnUnitDeployed;

        private CursorState _currentCursorState = CursorState.Invisible;
        private UiQueryData<CursorState> _cachedCursorState;
        private bool _suspendTroopPlacer;
        private bool _isMouseDown;
        private List<GameEntity> _orderPositionEntities;
        private List<GameEntity> _orderRotationEntities;
        private bool _formationDrawingMode;
        private Formation _mouseOverFormation;
        private Formation _clickedFormation;
        private Vec2 _lastMousePosition;
        private Vec2 _deltaMousePosition;
        private int _mouseOverDirection;
        private WorldPosition? _formationDrawingStartingPosition;
        private Vec2? _formationDrawingStartingPointOfMouse;
        private float? _formationDrawingStartingTime;
        private OrderController PlayerOrderController;
        private Team PlayerTeam;
        public bool Initialized;
        private Timer formationDrawTimer;
        public bool IsDrawingForced;
        public bool IsDrawingFacing;
        public bool IsDrawingForming;
        public bool IsDrawingAttaching;
        private bool _wasDrawingForced;
        private bool _wasDrawingFacing;
        private bool _wasDrawingForming;
        private GameEntity attachArrow;
        private float attachArrowLength;
        private GameEntity widthEntityLeft;
        private GameEntity widthEntityRight;
        private bool isDrawnThisFrame;
        private bool wasDrawnPreviousFrame;
        private static Material _meshMaterial;
        private bool _restrictOrdersToDeploymentBoundaries;

        private bool IsDeployment
        {
            get
            {
                return Mission.GetMissionBehavior<SiegeDeploymentHandler>() != null || Mission.GetMissionBehavior<BattleDeploymentHandler>() != null;
            }
        }

        public bool SuspendTroopPlacer
        {
            get => _suspendTroopPlacer;
            set
            {
                _suspendTroopPlacer = value;
                if (value)
                    HideOrderPositionEntities();
                else
                    _formationDrawingStartingPosition = new WorldPosition?();
                Reset();
            }
        }

        public Formation AttachTarget { get; private set; }

        public MovementOrder.Side AttachSide { get; private set; }

        public WorldPosition AttachPosition { get; private set; }

        public override void AfterStart()
        {
            base.AfterStart();
            _formationDrawingStartingPosition = null;
            _formationDrawingStartingPointOfMouse = null;
            _formationDrawingStartingTime = null;
            _orderRotationEntities = new List<GameEntity>();
            _orderPositionEntities = new List<GameEntity>();
            formationDrawTimer = new Timer(MBCommon.GetApplicationTime(), 0.0333333351f, true);
            widthEntityLeft = GameEntity.CreateEmpty(base.Mission.Scene, true);
            widthEntityLeft.AddComponent(MetaMesh.GetCopy("order_arrow_a", true, false));
            widthEntityLeft.SetVisibilityExcludeParents(false);
            widthEntityRight = GameEntity.CreateEmpty(base.Mission.Scene, true);
            widthEntityRight.AddComponent(MetaMesh.GetCopy("order_arrow_a", true, false));
            widthEntityRight.SetVisibilityExcludeParents(false);
        }

        private void InitializeInADisgustingManner()
        {
            PlayerTeam = Mission.PlayerTeam;
            PlayerOrderController = PlayerTeam.PlayerOrderController;
            _cachedCursorState = new UiQueryData<CursorState>(GetCursorState, 0.05f);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (Initialized)
                return;
            MissionPeer missionPeer = GameNetwork.IsMyPeerReady
                ? GameNetwork.MyPeer.GetComponent<MissionPeer>()
                : null;
            if (Mission.PlayerTeam == null && (missionPeer == null ||
                                                    missionPeer.Team != Mission.AttackerTeam &&
                                                    missionPeer.Team != Mission.DefenderTeam))
                return;
            InitializeInADisgustingManner();
            Initialized = true;
        }

        public void RestrictOrdersToDeploymentBoundaries(bool enabled)
        {
            _restrictOrdersToDeploymentBoundaries = enabled;
        }

        public void UpdateAttachVisuals(bool isVisible)
        {
            if (AttachTarget == null)
                isVisible = false;
            attachArrow.SetVisibilityExcludeParents(isVisible);
            if (isVisible)
            {
                Vec2 vec2 = AttachTarget.Direction;
                switch (AttachSide)
                {
                    case MovementOrder.Side.Front:
                        vec2 *= -1f;
                        break;
                    case MovementOrder.Side.Left:
                        vec2 = vec2.RightVec();
                        break;
                    case MovementOrder.Side.Right:
                        vec2 = vec2.LeftVec();
                        break;
                }

                float rotationInRadians = vec2.RotationInRadians;
                Mat3 identity1 = Mat3.Identity;
                identity1.RotateAboutUp(rotationInRadians);
                MatrixFrame identity2 = MatrixFrame.Identity;
                identity2.rotation = identity1;
                identity2.origin = AttachPosition.GetGroundVec3();
                identity2.Advance(-attachArrowLength);
                attachArrow.SetFrame(ref identity2);
            }

            if (!isVisible)
                return;
            MissionScreen.GetOrderFlagPosition();
            UpdateAttachData();
        }

        private void UpdateFormationDrawingForFacingOrder(bool giveOrder)
        {
            isDrawnThisFrame = true;
            Vec3 vec = MissionScreen.GetOrderFlagPosition();
            Vec2 asVec = vec.AsVec2;
            Vec2 orderLookAtDirection = OrderController.GetOrderLookAtDirection(PlayerOrderController.SelectedFormations, asVec);
            PlayerOrderController.SimulateNewFacingOrder(orderLookAtDirection, out List<WorldPosition> list);
            int num = 0;
            HideOrderPositionEntities();
            foreach (WorldPosition worldPosition in list)
            {
                int entityIndex = num;
                vec = worldPosition.GetGroundVec3();
                AddOrderPositionEntity(entityIndex, vec, giveOrder, -1f);
                num++;
            }
        }

        private void UpdateFormationDrawingForDestination(bool giveOrder)
        {
            isDrawnThisFrame = true;
            PlayerOrderController.SimulateDestinationFrames(out List<WorldPosition> list, 3f);
            int num = 0;
            HideOrderPositionEntities();
            foreach (WorldPosition worldPosition in list)
            {
                int entityIndex = num;
                Vec3 groundVec = worldPosition.GetGroundVec3();
                AddOrderPositionEntity(entityIndex, groundVec, giveOrder, 0.7f);
                num++;
            }
        }

        private void UpdateFormationDrawingForFormingOrder(bool giveOrder)
        {
            isDrawnThisFrame = true;
            MatrixFrame orderFlagFrame = MissionScreen.GetOrderFlagFrame();
            Vec3 origin = orderFlagFrame.origin;
            Vec2 asVec = orderFlagFrame.rotation.f.AsVec2;
            float orderFormCustomWidth = OrderController.GetOrderFormCustomWidth(PlayerOrderController.SelectedFormations, origin);
            List<WorldPosition> list;
            PlayerOrderController.SimulateNewCustomWidthOrder(orderFormCustomWidth, out list);
            Formation formation = PlayerOrderController.SelectedFormations.MaxBy((Formation f) => f.CountOfUnits);
            int num = 0;
            HideOrderPositionEntities();
            foreach (WorldPosition worldPosition in list)
            {
                int entityIndex = num;
                Vec3 groundVec = worldPosition.GetGroundVec3();
                AddOrderPositionEntity(entityIndex, groundVec, giveOrder, -1f);
                num++;
            }
            float unitDiameter = formation.UnitDiameter;
            float interval = formation.Interval;
            int num2 = MathF.Max(0, (int)((orderFormCustomWidth - unitDiameter) / (interval + unitDiameter) + 1E-05f)) + 1;
            float num3 = (float)(num2 - 1) * (interval + unitDiameter);
            for (int i = 0; i < num2; i++)
            {
                Vec2 a = new Vec2((float)i * (interval + unitDiameter) - num3 / 2f, 0f);
                Vec2 v = asVec.TransformToParentUnitF(a);
                WorldPosition worldPosition2 = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, origin, false);
                worldPosition2.SetVec2(worldPosition2.AsVec2 + v);
                int entityIndex2 = num++;
                Vec3 groundVec = worldPosition2.GetGroundVec3();
                AddOrderPositionEntity(entityIndex2, groundVec, false, -1f);
            }
        }

        private bool IsDraggingFormation()
        {
            if (_formationDrawingStartingPointOfMouse.HasValue)
            {
                Vec2 vec2 = _formationDrawingStartingPointOfMouse.Value - Input.GetMousePositionPixel();
                if (Math.Abs(vec2.x) >= 10.0 || Math.Abs(vec2.y) >= 10.0)
                {
                    return true;
                }
            }

            if (_formationDrawingStartingTime.HasValue && MBCommon.GetApplicationTime() - _formationDrawingStartingTime.Value >= 0.300000011920929)
            {
                return true;
            }

            return false;
        }

        private void UpdateFormationDrawing(bool giveOrder)
        {
            isDrawnThisFrame = true;
            HideOrderPositionEntities();
            if (_formationDrawingStartingPosition == null)
            {
                return;
            }
            WorldPosition worldPosition = WorldPosition.Invalid;
            bool flag = false;
            if (MissionScreen.MouseVisible && _formationDrawingStartingPointOfMouse != null)
            {
                Vec2 vec = _formationDrawingStartingPointOfMouse.Value - Input.GetMousePositionPixel();
                if (MathF.Abs(vec.x) < 10f && MathF.Abs(vec.y) < 10f)
                {
                    flag = true;
                    worldPosition = _formationDrawingStartingPosition.Value;
                }
            }
            if (MissionScreen.MouseVisible && _formationDrawingStartingTime != null && Mission.CurrentTime - _formationDrawingStartingTime.Value < 0.3f)
            {
                flag = true;
                worldPosition = _formationDrawingStartingPosition.Value;
            }
            if (!flag)
            {
                Vec3 vec2;
                Vec3 vec3;
                MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out vec2, out vec3);
                float f;
                if (!Mission.Scene.RayCastForClosestEntityOrTerrain(vec2, vec3, out f, 0.3f, BodyFlags.Disabled | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast | BodyFlags.BodyOwnerFlora))
                {
                    return;
                }
                Vec3 v = vec3 - vec2;
                v.Normalize();
                worldPosition = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, vec2 + v * f, false);
            }
            WorldPosition worldPosition2;
            if (_mouseOverDirection == 1)
            {
                worldPosition2 = worldPosition;
                worldPosition = _formationDrawingStartingPosition.Value;
            }
            else
            {
                worldPosition2 = _formationDrawingStartingPosition.Value;
            }
            if (!OrderFlag.IsPositionOnValidGround(worldPosition2))
            {
                return;
            }
            Vec2 vec4;
            if (_restrictOrdersToDeploymentBoundaries)
            {
                IMissionDeploymentPlan deploymentPlan = Mission.DeploymentPlan;
                BattleSideEnum side = Mission.PlayerTeam.Side;
                vec4 = worldPosition2.AsVec2;
                if (deploymentPlan.IsPositionInsideDeploymentBoundaries(side, vec4, DeploymentPlanType.Initial))
                {
                    IMissionDeploymentPlan deploymentPlan2 = Mission.DeploymentPlan;
                    BattleSideEnum side2 = Mission.PlayerTeam.Side;
                    Vec2 asVec = worldPosition.AsVec2;
                    if (deploymentPlan2.IsPositionInsideDeploymentBoundaries(side2, asVec, DeploymentPlanType.Initial))
                    {
                        goto IL_1DC;
                    }
                }
                return;
            }
        IL_1DC:
            bool isFormationLayoutVertical = !DebugInput.IsControlDown();
            UpdateFormationDrawingForMovementOrder(giveOrder, worldPosition2, worldPosition, isFormationLayoutVertical);
            Vec2 deltaMousePosition = _deltaMousePosition;
            float num = 1f;
            vec4 = Input.GetMousePositionRanged() - _lastMousePosition;
            _deltaMousePosition = deltaMousePosition * MathF.Max(num - vec4.Length * 10f, 0f);
            _lastMousePosition = Input.GetMousePositionRanged();
        }

        private void UpdateFormationDrawingForMovementOrder(
            bool giveOrder,
            WorldPosition formationRealStartingPosition,
            WorldPosition formationRealEndingPosition,
            bool isFormationLayoutVertical)
        {
            isDrawnThisFrame = true;
            PlayerOrderController.SimulateNewOrderWithPositionAndDirection(formationRealStartingPosition, formationRealEndingPosition, out List<WorldPosition> list, isFormationLayoutVertical);
            if (giveOrder)
            {
                if (!isFormationLayoutVertical)
                {
                    PlayerOrderController.SetOrderWithTwoPositions(OrderType.MoveToLineSegmentWithHorizontalLayout, formationRealStartingPosition, formationRealEndingPosition);
                }
                else
                {
                    PlayerOrderController.SetOrderWithTwoPositions(OrderType.MoveToLineSegment, formationRealStartingPosition, formationRealEndingPosition);
                }
            }
            int num = 0;
            foreach (WorldPosition worldPosition in list)
            {
                int entityIndex = num;
                Vec3 groundVec = worldPosition.GetGroundVec3();
                AddOrderPositionEntity(entityIndex, groundVec, giveOrder, -1f);
                num++;
            }
        }
        
        private void BeginFormationDraggingOrClicking()
        {
            Vec3 rayBegin;
            Vec3 rayEnd;
            MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out rayBegin, out rayEnd);
            float collisionDistance;
            if (Mission.Scene.RayCastForClosestEntityOrTerrain(rayBegin, rayEnd, out collisionDistance,
                0.3f))
            {
                Vec3 vec3 = rayEnd - rayBegin;
                double num = vec3.Normalize();
                _formationDrawingStartingPosition = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, rayBegin + vec3 * collisionDistance,
                    false);
                _formationDrawingStartingPointOfMouse = Input.GetMousePositionPixel();
                _formationDrawingStartingTime = MBCommon.GetApplicationTime();
                return;
            }

            _formationDrawingStartingPosition = new WorldPosition?();
            _formationDrawingStartingPointOfMouse = new Vec2?();
            _formationDrawingStartingTime = new float?();
        }

        private void HandleMousePressed()
        {
            if (PlayerOrderController.SelectedFormations.IsEmpty() || _clickedFormation != null)
                return;
            switch (_currentCursorState)
            {
                case CursorState.Enemy:
                    if (_config.AttackSpecificFormation && CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyDown(Input))
                    {
                        _clickedFormation = _mouseOverFormation;
                    }
                    else
                    {
                        _formationDrawingMode = true;
                    }
                    BeginFormationDraggingOrClicking();
                    break;
                case CursorState.Friend:
                    if (_config.ClickToSelectFormation && CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyDown(Input))
                    {
                        if (_mouseOverFormation != null && PlayerOrderController.IsFormationSelectable(_mouseOverFormation))
                        {
                            _clickedFormation = _mouseOverFormation;
                        }
                    }
                    else
                    {
                        _formationDrawingMode = true;
                    }
                    BeginFormationDraggingOrClicking();
                    break;
                case CursorState.Normal:
                    if (Input.IsKeyPressed(InputKey.LeftMouseButton))
                    {
                        _formationDrawingMode = true;
                        BeginFormationDraggingOrClicking();
                    }
                    break;
                case CursorState.Rotation:
                    if (_mouseOverFormation.CountOfUnits <= 0)
                        break;

                    HideNonSelectedOrderRotationEntities(_mouseOverFormation);
                    PlayerOrderController.ClearSelectedFormations();
                    PlayerOrderController.SelectFormation(_mouseOverFormation);

                    _formationDrawingMode = true;

                    WorldPosition orderWorldPosition = _mouseOverFormation.CreateNewOrderWorldPosition(WorldPosition.WorldPositionEnforcedCache.None);

                    Vec2 direction = _mouseOverFormation.Direction;
                    direction.RotateCCW(-1.570796f);

                    _formationDrawingStartingPosition = orderWorldPosition;
                    _formationDrawingStartingPosition.Value.SetVec2(_formationDrawingStartingPosition.Value.AsVec2 + direction * (_mouseOverDirection == 1 ? 0.5f : -0.5f) * _mouseOverFormation.Width);

                    orderWorldPosition.SetVec2(orderWorldPosition.AsVec2 + direction * (_mouseOverDirection == 1 ? -0.5f : 0.5f) * _mouseOverFormation.Width);

                    _deltaMousePosition = MissionScreen.SceneView.WorldPointToScreenPoint(orderWorldPosition.GetGroundVec3()) - GetScreenPoint();
                    _lastMousePosition = Input.GetMousePositionRanged();
                    break;
            }
        }

        private void TryTransformFromClickingToDragging()
        {
            if (PlayerOrderController.SelectedFormations.IsEmpty())
                return;
            switch (_currentCursorState)
            {
                case CursorState.Enemy:
                case CursorState.Friend:
                    if (IsDraggingFormation())
                    {
                        _formationDrawingMode = true;
                        _clickedFormation = null;
                    }

                    break;
            }
        }

        private void HandleMouseDown()
        {
            if (!PlayerOrderController.SelectedFormations.IsEmpty<Formation>() && _clickedFormation == null)
            {
                switch (GetCursorState())
                {
                    case CursorState.Invisible:
                    case CursorState.Ground:
                        break;
                    case CursorState.Normal:
                        {
                            _formationDrawingMode = true;
                            MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out Vec3 vec, out Vec3 vec2);
                            if (Mission.Scene.RayCastForClosestEntityOrTerrain(vec, vec2, out float f, 0.3f, BodyFlags.Disabled | BodyFlags.AILimiter | BodyFlags.Barrier | BodyFlags.Barrier3D | BodyFlags.Ragdoll | BodyFlags.RagdollLimiter | BodyFlags.DoNotCollideWithRaycast | BodyFlags.BodyOwnerFlora))
                            {
                                Vec3 v = vec2 - vec;
                                v.Normalize();
                                _formationDrawingStartingPosition = new WorldPosition?(new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, vec + v * f, false));
                                _formationDrawingStartingPointOfMouse = new Vec2?(Input.GetMousePositionPixel());
                                _formationDrawingStartingTime = new float?(Mission.CurrentTime);
                                return;
                            }
                            _formationDrawingStartingPosition = null;
                            _formationDrawingStartingPointOfMouse = null;
                            _formationDrawingStartingTime = null;
                            return;
                        }
                    case CursorState.Enemy:
                    case CursorState.Friend:
                        _clickedFormation = _mouseOverFormation;
                        return;
                    case CursorState.Rotation:
                        if (_mouseOverFormation.CountOfUnits > 0)
                        {
                            HideNonSelectedOrderRotationEntities(_mouseOverFormation);
                            PlayerOrderController.ClearSelectedFormations();
                            PlayerOrderController.SelectFormation(_mouseOverFormation);
                            _formationDrawingMode = true;
                            WorldPosition worldPosition = _mouseOverFormation.CreateNewOrderWorldPosition(WorldPosition.WorldPositionEnforcedCache.GroundVec3);
                            Vec2 direction = _mouseOverFormation.Direction;
                            direction.RotateCCW(-1.57079637f);
                            _formationDrawingStartingPosition = new WorldPosition?(worldPosition);
                            _formationDrawingStartingPosition.Value.SetVec2(_formationDrawingStartingPosition.Value.AsVec2 + direction * ((_mouseOverDirection == 1) ? 0.5f : -0.5f) * _mouseOverFormation.Width);
                            WorldPosition worldPosition2 = worldPosition;
                            worldPosition2.SetVec2(worldPosition2.AsVec2 + direction * ((_mouseOverDirection == 1) ? -0.5f : 0.5f) * _mouseOverFormation.Width);
                            Vec2 v2 = MissionScreen.SceneView.WorldPointToScreenPoint(worldPosition2.GetGroundVec3());
                            Vec2 screenPoint = GetScreenPoint();
                            _deltaMousePosition = v2 - screenPoint;
                            _lastMousePosition = Input.GetMousePositionRanged();
                        }
                        break;
                    default:
                        return;
                }
            }
        }

        private void HandleMouseUp()
        {
            var cursorState = _currentCursorState;
            if (_clickedFormation != null && CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyDown(Input))
            {
                if (_clickedFormation.CountOfUnits > 0)
                {
                    bool isEnemy = MissionSharedLibrary.Utilities.Utility.IsEnemy(_clickedFormation);
                    if (!isEnemy)
                    {
                        HideNonSelectedOrderRotationEntities(_clickedFormation);

                        if (PlayerOrderController.IsFormationSelectable(_clickedFormation))
                        {
                            if (!Input.IsControlDown())
                            {
                                PlayerOrderController.ClearSelectedFormations();
                                PlayerOrderController.SelectFormation(_clickedFormation);
                            }
                            else if (PlayerOrderController.IsFormationListening(_clickedFormation))
                            {
                                PlayerOrderController.DeselectFormation(_clickedFormation);
                            }
                            else
                            {
                                PlayerOrderController.SelectFormation(_clickedFormation);
                            }
                        }
                    }
                    else if (_config.AttackSpecificFormation)
                    {
                        PlayerOrderController.SetOrderWithFormation(OrderType.ChargeWithTarget, _clickedFormation);
                        Utility.DisplayChargeToFormationMessage(PlayerOrderController.SelectedFormations,
                            _clickedFormation);
                    }
                }

                _clickedFormation = null;
            }
            else if (cursorState == CursorState.Ground)
            {
                if (IsDrawingFacing || _wasDrawingFacing)
                    UpdateFormationDrawingForFacingOrder(true);
                else if (IsDrawingForming || _wasDrawingForming)
                    UpdateFormationDrawingForFormingOrder(true);
                else
                    UpdateFormationDrawing(true);
                if (IsDeployment)
                    SoundEvent.PlaySound2D("event:/ui/mission/deploy");
            }

            _formationDrawingMode = false;
            _formationDrawingStartingPosition = null;
            _formationDrawingStartingPointOfMouse = null;
            _formationDrawingStartingTime = null;
            _deltaMousePosition = Vec2.Zero;
        }

        private Vec2 GetScreenPoint()
        {
            if (!MissionScreen.MouseVisible)
            {
                return new Vec2(0.5f, 0.5f) + _deltaMousePosition;
            }
            return Input.GetMousePositionRanged() + _deltaMousePosition;
        }

        private CursorState GetCursorState()
        {
            CursorState cursorState = CursorState.Invisible;
            AttachTarget = null;
            if (!PlayerOrderController.SelectedFormations.IsEmpty() && _clickedFormation == null)
            {
                MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out var rayBegin, out var rayEnd);
                if (!Mission.Scene.RayCastForClosestEntityOrTerrain(rayBegin, rayEnd, out var collisionDistance,
                    out GameEntity collidedEntity, 0.3f))
                    collisionDistance = 1000f;
                if (cursorState == CursorState.Invisible && collisionDistance < 1000.0)
                {
                    if (!_formationDrawingMode && collidedEntity == null)
                    {
                        for (int index = 0; index < _orderRotationEntities.Count; ++index)
                        {
                            GameEntity orderRotationEntity = _orderRotationEntities[index];
                            if (orderRotationEntity.IsVisibleIncludeParents() &&
                                collidedEntity == orderRotationEntity)
                            {
                                _mouseOverFormation =
                                    PlayerOrderController.SelectedFormations.ElementAt(index / 2);
                                _mouseOverDirection = 1 - (index & 1);
                                cursorState = CursorState.Rotation;
                                break;
                            }
                        }
                    }

                    if (cursorState == CursorState.Invisible)
                    {
                        if (MissionScreen.OrderFlag.FocusedOrderableObject != null)
                            cursorState = CursorState.OrderableEntity;
                        else if (_config.ShouldHighlightWithOutline())
                        {
                            var formation = GetMouseOverFormation(collisionDistance);
                            _mouseOverFormation = formation;
                            if (formation != null)
                            {
                                if (formation.Team.IsEnemyOf(Mission.PlayerTeam))
                                {
                                    if (_config.AttackSpecificFormation)
                                    {
                                        cursorState = CursorState.Enemy;
                                    }
                                }
                                else
                                {
                                    if (_config.ClickToSelectFormation)
                                    {
                                        cursorState = CursorState.Friend;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (_clickedFormation != null) // click on formation and hold.
            {
                cursorState = _currentCursorState;
            }

            if (cursorState == CursorState.Invisible &&
                !(CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyDown(Input) && _config.ShouldHighlightWithOutline()) || // press middle mouse button to avoid accidentally click on ground.
                _formationDrawingMode)
            {
                cursorState = IsCursorStateGroundOrNormal();
                UpdateAttachData();
            }

            if (cursorState != CursorState.Ground &&
                cursorState != CursorState.Rotation)
                _mouseOverDirection = 0;
            return cursorState;
        }

        private CursorState IsCursorStateGroundOrNormal()
        {
            if (!_formationDrawingMode)
            {
                return CursorState.Normal;
            }
            return CursorState.Ground;
        }

        private void UpdateAttachData()
        {
            if (!IsDrawingForced)
                return;

            Vec3 orderFlagPosition = MissionScreen.GetOrderFlagPosition();
            foreach (Formation formation in PlayerTeam.Formations.Where(f => !PlayerOrderController.IsFormationListening(f)))
            {
                WorldPosition worldPosition;
                Vec2 asVec2;
                if (AttachTarget != null)
                {
                    worldPosition = formation.QuerySystem.MedianPosition;
                    asVec2 = worldPosition.AsVec2;
                    double num1 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                    worldPosition = AttachPosition;

                    asVec2 = worldPosition.AsVec2;
                    double num2 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);

                    if (num1 >= num2)
                        goto label_7;
                }

                AttachTarget = formation;
                AttachSide = MovementOrder.Side.Rear;
                AttachPosition = formation.QuerySystem.MedianPosition;
            label_7:
                worldPosition = formation.QuerySystem.MedianPosition;
                asVec2 = worldPosition.AsVec2;
                double num3 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);

                worldPosition = AttachPosition;
                asVec2 = worldPosition.AsVec2;
                double num4 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                if (num3 < num4)
                {
                    AttachTarget = formation;
                    AttachSide = MovementOrder.Side.Left;
                    AttachPosition = formation.QuerySystem.MedianPosition;
                }

                worldPosition = formation.QuerySystem.MedianPosition;
                asVec2 = worldPosition.AsVec2;
                double num5 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                worldPosition = AttachPosition;
                asVec2 = worldPosition.AsVec2;
                double num6 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                if (num5 < num6)
                {
                    AttachTarget = formation;
                    AttachSide = MovementOrder.Side.Right;
                    AttachPosition = formation.QuerySystem.MedianPosition;
                }

                worldPosition = formation.QuerySystem.MedianPosition;
                asVec2 = worldPosition.AsVec2;
                double num7 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                worldPosition = AttachPosition;
                asVec2 = worldPosition.AsVec2;
                double num8 = asVec2.DistanceSquared(orderFlagPosition.AsVec2);
                if (num7 < num8)
                {
                    AttachTarget = formation;
                    AttachSide = MovementOrder.Side.Front;
                    AttachPosition = formation.QuerySystem.MedianPosition;
                }
            }
        }

        private void AddOrderPositionEntity(
            int entityIndex, 
            in Vec3 groundPosition, 
            bool fadeOut, 
            float alpha = -1f)
        {
            while (_orderPositionEntities.Count <= entityIndex)
            {
                GameEntity gameEntity = GameEntity.CreateEmpty(Mission.Scene, true);
                gameEntity.EntityFlags |= EntityFlags.NotAffectedBySeason;
                MetaMesh copy = MetaMesh.GetCopy("order_flag_small", true, false);
                if (_meshMaterial == null)
                {
                    _meshMaterial = copy.GetMeshAtIndex(0).GetMaterial().CreateCopy();
                    _meshMaterial.SetAlphaBlendMode(Material.MBAlphaBlendMode.Factor);
                }
                copy.SetMaterial(_meshMaterial);
                gameEntity.AddComponent(copy);
                gameEntity.SetVisibilityExcludeParents(false);
                _orderPositionEntities.Add(gameEntity);
            }
            GameEntity gameEntity2 = _orderPositionEntities[entityIndex];
            MatrixFrame matrixFrame = new MatrixFrame(Mat3.Identity, groundPosition);
            gameEntity2.SetFrame(ref matrixFrame);
            if (alpha != -1f)
            {
                gameEntity2.SetVisibilityExcludeParents(true);
                gameEntity2.SetAlpha(alpha);
                return;
            }
            if (fadeOut)
            {
                gameEntity2.FadeOut(0.3f, false);
                return;
            }
            gameEntity2.FadeIn(true);
        }

        private void HideNonSelectedOrderRotationEntities(Formation formation)
        {
            for (int i = 0; i < _orderRotationEntities.Count; i++)
            {
                GameEntity gameEntity = _orderRotationEntities[i];
                if (gameEntity == null && gameEntity.IsVisibleIncludeParents() && 
                    PlayerOrderController.SelectedFormations.ElementAt(i / 2) != formation)
                {
                    gameEntity.SetVisibilityExcludeParents(false);
                    gameEntity.BodyFlag |= BodyFlags.Disabled;
                }
            }
        }

        private void HideOrderPositionEntities()
        {
            foreach (GameEntity gameEntity in _orderPositionEntities)
            {
                gameEntity.HideIfNotFadingOut();
            }
            for (int i = 0; i < _orderRotationEntities.Count; i++)
            {
                GameEntity gameEntity2 = _orderRotationEntities[i];
                gameEntity2.SetVisibilityExcludeParents(false);
                gameEntity2.BodyFlag |= BodyFlags.Disabled;
            }
        }

        [Conditional("DEBUG")]
        private void DebugTick(float dt)
        {
            int num = Initialized ? 1 : 0;
        }

        private void Reset()
        {
            _isMouseDown = false;
            _formationDrawingMode = false;
            _formationDrawingStartingPosition = null;
            _formationDrawingStartingPointOfMouse = null;
            _formationDrawingStartingTime = null;
            _mouseOverFormation = null;
            _clickedFormation = null;
        }

        public override void OnMissionScreenTick(float dt)
        {
            if (!Initialized)
                return;
            base.OnMissionScreenTick(dt);
            if (!PlayerOrderController.SelectedFormations.Any())
                return;
            isDrawnThisFrame = false;
            if (SuspendTroopPlacer)
                return;

            _currentCursorState = _cachedCursorState.Value;
            //Utilities.DisplayMessage(_currentCursorState.ToString());
            // use middle mouse button to select formation
            if (Input.IsKeyPressed(InputKey.LeftMouseButton) || _config.ShouldHighlightWithOutline() && CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyPressed(Input))
            {
                _isMouseDown = true;
                HandleMousePressed();
                //Utilities.DisplayMessage("key pressed");
            }

            if ((Input.IsKeyReleased(InputKey.LeftMouseButton) ||
                 _config.ShouldHighlightWithOutline() && CommandSystemGameKeyCategory.GetKey(GameKeyEnum.SelectFormation).IsKeyPressed(Input) &&
                 !_formationDrawingMode) && _isMouseDown)
            {
                _isMouseDown = false;
                HandleMouseUp();
                //Utilities.DisplayMessage("key up");
            }
            else if (Input.IsKeyDown(InputKey.LeftMouseButton) && _isMouseDown)
            {
                //Utilities.DisplayMessage("key down");
                if (formationDrawTimer.Check(MBCommon.GetApplicationTime()) &&
                    !IsDrawingFacing &&
                    !IsDrawingForming)
                {
                    //Utilities.DisplayMessage("try transform");
                    TryTransformFromClickingToDragging();
                    if (_currentCursorState == CursorState.Ground)
                        UpdateFormationDrawing(false);
                }
            }
            else if (IsDrawingForced)
            {
                //Utilities.DisplayMessage("drawing forced");
                Reset();
                _formationDrawingMode = true;
                BeginFormationDraggingOrClicking();
                //HandleMousePressed();
                UpdateFormationDrawing(false);
            }
            else if (IsDrawingFacing || _wasDrawingFacing)
            {
                if (IsDrawingFacing)
                {
                    Reset();
                    UpdateFormationDrawingForFacingOrder(false);
                }
            }
            else if (IsDrawingForming || _wasDrawingForming)
            {
                if (IsDrawingForming)
                {
                    Reset();
                    UpdateFormationDrawingForFormingOrder(false);
                }
            }
            else if (_wasDrawingForced)
                Reset();
            else
            {
                UpdateFormationDrawingForDestination(false);
            }
            UpdateInputForContour();



            foreach (GameEntity orderPositionEntity in _orderPositionEntities)
                orderPositionEntity.SetPreviousFrameInvalid();
            foreach (GameEntity orderRotationEntity in _orderRotationEntities)
                orderRotationEntity.SetPreviousFrameInvalid();
            _wasDrawingForced = IsDrawingForced;
            _wasDrawingFacing = IsDrawingFacing;
            _wasDrawingForming = IsDrawingForming;
            wasDrawnPreviousFrame = isDrawnThisFrame;
        }

        private void UpdateInputForContour()
        {
            _contourView?.MouseOver(_mouseOverFormation);
        }

        private Agent RayCastForAgent(float distance)
        {
            MissionScreen.ScreenPointToWorldRay(GetScreenPoint(), out var rayBegin, out var rayEnd);
            var agent = Mission.RayCastForClosestAgent(rayBegin, rayEnd, out var agentDistance,
                MissionScreen.LastFollowedAgent?.Index ?? -1, 0.8f);
            return agentDistance > distance ? null : agent;
        }

        private Formation GetMouseOverFormation(float collisionDistance)
        {
            var agent = RayCastForAgent(collisionDistance);
            if (agent != null && agent.IsMount)
                agent = agent.RiderAgent;
            if (agent == null)
                return null;
            if (_config.ShouldHighlightWithOutline() && !IsDrawingForced && !_formationDrawingMode && agent?.Formation != null &&
                !(PlayerOrderController.SelectedFormations.Count == 1 &&
                  PlayerOrderController.SelectedFormations.Contains(agent.Formation)))
            {
                return agent.Formation;
            }

            return null;
        }

        protected enum CursorState
        {
            Invisible,
            Normal,
            Ground,
            Enemy,
            Friend,
            Rotation,
            Count,
            OrderableEntity
        }
    }
}
