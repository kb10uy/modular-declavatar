using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace KusakaFactory.Declavatar.EditorExtension
{
    public class BuildLogWindow : EditorWindow
    {
        private List<(ErrorKind, string)> _logs = new List<(ErrorKind, string)>();

        private Vector2 _windowErrorsScroll = Vector2.zero;

        #region External Methods

        internal static BuildLogWindow ShowLogWindow()
        {
            return GetWindow<BuildLogWindow>("Declavatar Build Log", true);
        }

        internal void Clear()
        {
            _logs.Clear();
            Repaint();
        }

        internal void SetLog(IEnumerable<(ErrorKind, string)> logs)
        {
            _logs.Clear();
            _logs.AddRange(logs);
            Repaint();
        }

        internal void AddLog(ErrorKind kind, string message)
        {
            _logs.Add((kind, message));
            Repaint();
        }

        #endregion

        #region UI Drawing

        public void OnGUI()
        {
            DrawHeader();
            DrawLogs();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Declavatar", Constants.TitleLabel);
            GUILayout.Label("Declarative Avatar Assets Composing Tool, by kb10uy", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
        }

        private void DrawLogs()
        {
            GUILayout.Label("Errors", Constants.BigBoldLabel);
            _windowErrorsScroll = GUILayout.BeginScrollView(_windowErrorsScroll, Constants.MarginBox);
            if (_logs.Count > 0)
            {
                var previousBackground = GUI.backgroundColor;
                var iconStyle = new GUIStyle() { padding = new RectOffset(4, 0, 4, 4) };
                foreach (var (kind, message) in _logs)
                {
                    GUILayout.BeginHorizontal();

                    GUIContent icon = null;
                    switch (kind)
                    {
                        case ErrorKind.CompilerError:
                        case ErrorKind.SyntaxError:
                        case ErrorKind.SemanticError:
                        case ErrorKind.RuntimeError:
                            icon = EditorGUIUtility.IconContent("console.erroricon");
                            break;
                        case ErrorKind.SemanticInfo:
                            icon = EditorGUIUtility.IconContent("console.infoicon");
                            break;
                    }
                    GUILayout.Box(icon, iconStyle);

                    GUILayout.BeginVertical();
                    GUILayout.Space(4.0f);
                    GUILayout.Label($"{kind}", EditorStyles.boldLabel);
                    GUILayout.Label(message);
                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUI.backgroundColor = previousBackground;
            }
            else
            {
                GUILayout.Label($"No errors detected.");
            }
            GUILayout.EndScrollView();
        }

        #endregion
    }
}
