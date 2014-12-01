﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitMerge
{
    /// <summary>
    /// One instance of this class represents one GameObject with relevance to the merge process.
    /// Holds all MergeActions that can be applied to the GameObject or its Components.
    /// Is considered as "merged" when all its MergeActions are "merged".
    /// </summary>
    public class GameObjectMergeActions
    {
        /// <summary>
        /// Reference to "our" version of the GameObject.
        /// </summary>
        public GameObject ours { private set; get; }
        /// <summary>
        /// Reference to "their" versoin of the GameObject.
        /// </summary>
        public GameObject theirs { private set; get; }

        private string name;
        public bool merged { private set; get; }
        public bool hasActions
        {
            get { return actions.Count > 0; }
        }
        /// <summary>
        /// All actions available for solving specific conflicts on the GameObject.
        /// </summary>
        private List<MergeAction> actions;


        public GameObjectMergeActions(GameObject ours, GameObject theirs)
        {
            actions = new List<MergeAction>();

            this.ours = ours;
            this.theirs = theirs;
            GenerateName();

            if(theirs && !ours)
            {
                actions.Add(new MergeActionNewGameObject(ours, theirs));
            }
            if(ours && !theirs)
            {
                actions.Add(new MergeActionDeleteGameObject(ours, theirs));
            }
            if(ours && theirs)
            {
                FindComponentDifferences();
            }

            //Some Actions have a default and are merged from the beginning.
            //If all the others did was to add GameObjects, we're done with merging from the start.
            CheckIfMerged();
        }

        /// <summary>
        /// Generate a title for this object
        /// </summary>
        private void GenerateName()
        {
            name = "";
            if(ours)
            {
                name = "Your[" + GetPath(ours) + "]";
            }
            if(theirs)
            {
                if(ours)
                {
                    name += " vs. ";
                }
                name += "Their[" + GetPath(theirs) + "]";
            }
        }

        /// <summary>
        /// Check for Components that one of the sides doesn't have, and/or for defferent values
        /// on Components.
        /// </summary>
        private void FindComponentDifferences()
        {
            var ourComponents = ours.GetComponents<Component>();
            var theirComponents = theirs.GetComponents<Component>();

            //Map "their" Components to their respective ids
            var theirDict = new Dictionary<int, Component>();
            foreach(var theirComponent in theirComponents)
            {
                theirDict.Add(ObjectIDFinder.GetIdentifierFor(theirComponent), theirComponent);
            }

            foreach(var ourComponent in ourComponents)
            {
                //Try to find "their" equivalent to our Components
                var id = ObjectIDFinder.GetIdentifierFor(ourComponent);
                Component theirComponent;
                theirDict.TryGetValue(id, out theirComponent);

                if(theirComponent) //Both Components exist
                {
                    FindPropertyDifferences(ourComponent, theirComponent);
                    //Remove "their" Component from the dict to only keep those new to us
                    theirDict.Remove(id);
                }
                else //Component doesn't exist in their version, offer a deletion
                {
                    actions.Add(new MergeActionDeleteComponent(ours, ourComponent));
                }
            }

            //Everything left in the dict is a...
            foreach(var theirComponent in theirDict.Values)
            {
                //...new Component from them
                actions.Add(new MergeActionNewComponent(ours, theirComponent));
            }
        }

        /// <summary>
        /// Find all the values different in "our" and "their" version of a component.
        /// </summary>
        private void FindPropertyDifferences(Component ourComponent, Component theirComponent)
        {
            var ourSerialized = new SerializedObject(ourComponent);
            var theirSerialized = new SerializedObject(theirComponent);

            var ourProperty = ourSerialized.GetIterator();
            if(ourProperty.Next(true))
            {
                var theirProperty = theirSerialized.GetIterator();
                theirProperty.Next(true);
                while(ourProperty.NextVisible(false))
                {
                    theirProperty.NextVisible(false);

                    if(!ourProperty.GetValue(true).Equals(theirProperty.GetValue(true)))
                    {
                        //We found a difference, accordingly add a MergeAction
                        actions.Add(new MergeActionChangeValues(ours, ourComponent, ourProperty.Copy(), theirProperty.Copy()));
                    }
                }
            }
        }

        /// <summary>
        /// Get the path of a GameObject in the hierarchy.
        /// </summary>
        private static string GetPath(GameObject g)
        {
            var t = g.transform;
            var sb = new StringBuilder(t.name);
            while(t.parent != null)
            {
                t = t.parent;
                sb.Append(t.name + "/", 0, 1);
            }
            return sb.ToString();
        }

        private void CheckIfMerged()
        {
            merged = actions.TrueForAll(action => action.merged);
        }

        /// <summary>
        /// Use "our" version for all conflicts.
        /// This is used on all GameObjectMergeActions objects when the merge is aborted.
        /// </summary>
        public void UseOurs()
        {
            foreach(var action in actions)
            {
                action.UseOurs();
            }
        }

        //If the foldout is open
        private bool open;
        public void OnGUI()
        {
            if(open)
            {
                GUI.backgroundColor = new Color(0, 0, 0, .8f);
            }
            else
            {
                GUI.backgroundColor = merged ? new Color(0, .5f, 0, .8f) : new Color(.5f, 0, 0, .8f);
            }
            GUILayout.BeginVertical(Resources.styles.mergeActions);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginHorizontal();
            open = EditorGUILayout.Foldout(open, new GUIContent(name));

            if(ours && GUILayout.Button("Focus", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                ours.Highlight();
            }
            GUILayout.EndHorizontal();

            if(open)
            {
                //Display all merge actions.
                foreach(var action in actions)
                {
                    if(action.OnGUIMerge())
                    {
                        CheckIfMerged();
                    }
                }
            }

            //If "ours" is null, the GameObject doesn't exist in one of the versions.
            //Try to get a reference if the object exists in the current merging state.
            //If it exists, the new/gelete MergeAction will have a reference.
            if(!ours)
            {
                foreach(var action in actions)
                {
                    ours = action.ours;
                }
            }

            GUILayout.EndVertical();

            GUI.backgroundColor = Color.white;
        }
    }
}