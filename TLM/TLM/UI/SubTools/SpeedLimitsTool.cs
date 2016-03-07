﻿using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI.SubTools {
	public class SpeedLimitsTool : SubTool {
		private bool _cursorInSecondaryPanel;
		private int curSpeedLimitIndex = 0;
		private bool overlayHandleHovered;
		private static Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>> segmentCenterByDir = new Dictionary<ushort, Dictionary<NetInfo.Direction, Vector3>>();
		private static float speedLimitSignSize = 80f;
		private Texture2D SecondPanelTexture;

		public SpeedLimitsTool(TrafficManagerTool mainTool) : base(mainTool) {
			SecondPanelTexture = TrafficManagerTool.MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 1f));
		}

		public override bool IsCursorInPanel() {
			return _cursorInSecondaryPanel;
		}

		public override void OnClickOverlay() {
			
		}

		public override void OnToolGUI(Event e) {
			_cursorInSecondaryPanel = false;

			var style = new GUIStyle {
				normal = { background = SecondPanelTexture },
				alignment = TextAnchor.MiddleCenter,
				border =
				{
					bottom = 2,
					top = 2,
					right = 2,
					left = 2
				}
			};

			var windowRect = TrafficManagerTool.ResizeGUI(new Rect(155, 45, 7 * 105, 210));
			GUILayout.Window(254, windowRect, _guiSpeedLimitsWindow, Translation.GetString("Speed_limits"), style);
			_cursorInSecondaryPanel = windowRect.Contains(Event.current.mousePosition);
			overlayHandleHovered = false;
			ShowSigns(false);
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			
		}

		public override void ShowIcons() {
			ShowSigns(true);
		}

		public override void Cleanup() {
			segmentCenterByDir.Clear();
		}

		private void ShowSigns(bool viewOnly) {
			Array16<NetSegment> segments = Singleton<NetManager>.instance.m_segments;
			bool handleHovered = false;
			for (int i = 1; i < segments.m_size; ++i) {
				if (segments.m_buffer[i].m_flags == NetSegment.Flags.None) // segment is unused
					continue;
#if !DEBUG
				if ((segments.m_buffer[i].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
					continue;
#endif
				var segmentInfo = segments.m_buffer[i].Info;

				Vector3 centerPos = segments.m_buffer[i].m_bounds.center;
				var screenPos = Camera.main.WorldToScreenPoint(centerPos);
				screenPos.y = Screen.height - screenPos.y;

				if (screenPos.z < 0)
					continue;

				// draw speed limits
				if (TrafficManagerTool.GetToolMode() != ToolMode.VehicleRestrictions || i != SelectedSegmentId) { // no speed limit overlay on selected segment when in vehicle restrictions mode
					if (drawSpeedLimitHandles((ushort)i, viewOnly))
						handleHovered = true;
				}
			}
			overlayHandleHovered = handleHovered;
		}

		private void _guiSpeedLimitsWindow(int num) {
			GUILayout.BeginHorizontal();

			Color oldColor = GUI.color;
			for (int i = 0; i < SpeedLimitManager.AvailableSpeedLimits.Count; ++i) {
				if (curSpeedLimitIndex != i)
					GUI.color = Color.gray;
				if (GUILayout.Button(TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.AvailableSpeedLimits[i]], GUILayout.Width(100), GUILayout.Height(100))) {
					curSpeedLimitIndex = i;
				}
				GUI.color = oldColor;

				if (i == 6) {
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();
				}
			}

			GUILayout.EndHorizontal();
		}

		private bool drawSpeedLimitHandles(ushort segmentId, bool viewOnly) {
			if (!LoadingExtension.IsPathManagerCompatible) {
				return false;
			}

			if (viewOnly && !Options.speedLimitsOverlay)
				return false;

			// draw speedlimits over mean middle points of lane beziers
			if (!segmentCenterByDir.ContainsKey(segmentId)) {
				segmentCenterByDir.Add(segmentId, new Dictionary<NetInfo.Direction, Vector3>());
				TrafficManagerTool.CalculateSegmentCenterByDir(segmentId, segmentCenterByDir[segmentId]);
			}

			bool hovered = false;
			foreach (KeyValuePair<NetInfo.Direction, Vector3> e in segmentCenterByDir[segmentId]) {
				Vector3 signPos = e.Value;
				var screenPos = Camera.main.WorldToScreenPoint(signPos);
				screenPos.y = Screen.height - screenPos.y;
				if (screenPos.z < 0)
					return false;
				var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
				var diff = signPos - camPos;

				if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
					return false; // do not draw if too distant

				ItemClass connectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.GetConnectionClass();
				if (!(connectionClass.m_service == ItemClass.Service.Road ||
					(connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain)))
					return false;

				var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
				var size = speedLimitSignSize * zoom;
				var guiColor = GUI.color;
				var boundingBox = new Rect(screenPos.x - size / 2, screenPos.y - size / 2, size, size);
				bool hoveredHandle = !viewOnly && TrafficManagerTool.IsMouseOver(boundingBox);

				if (hoveredHandle) {
					// mouse hovering over sign
					hovered = true;
					guiColor.a = 0.8f;
				} else {
					guiColor.a = 0.5f;
				}

				GUI.color = guiColor;

				try {
					GUI.DrawTexture(boundingBox, TrafficLightToolTextureResources.SpeedLimitTextures[SpeedLimitManager.GetCustomSpeedLimit(segmentId, e.Key)]);
				} catch (Exception ex) {
					Log.Error("segment " + segmentId + " limit: " + SpeedLimitManager.GetCustomSpeedLimit(segmentId, e.Key) + ", ex: " + ex.ToString());
				}

				if (hoveredHandle && Input.GetMouseButton(0)) {
					// change the speed limit to the selected one
					ushort speedLimitToSet = SpeedLimitManager.AvailableSpeedLimits[curSpeedLimitIndex];
					//Log._Debug($"Setting speed limit of segment {segmentId}, dir {e.Key.ToString()} to {speedLimitToSet}");
					SpeedLimitManager.SetSpeedLimit(segmentId, e.Key, speedLimitToSet);
					//mouseClickProcessed = true;
				}

				guiColor.a = 1f;
				GUI.color = guiColor;
			}
			return hovered;
		}
	}
}
