﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerrariaUI.Widgets.Media;

namespace TerrariaUI
{
    public class ApplicationType
    {
        public string Name { get; protected set; }
        public Func<string, HashSet<int>, Application> Generator { get; protected set; }
        public bool AllowManualRun { get; protected set; }
        protected Dictionary<int, Application> Instances { get; set; } = new Dictionary<int, Application>();
        protected ApplicationSaver Saver;
        protected object Locker = new object();

        public ImageData Icon { get; set; } = null;

        public int InstancesCount
        {
            get
            {
                lock (Locker)
                    return Instances.Count;
            }
        }
        public IEnumerable<KeyValuePair<int, Application>> IterateInstances
        {
            get
            {
                lock (Locker)
                    foreach (var instance in Instances.ToArray())
                        yield return instance;
            }
        }

        public ApplicationType(string name, Func<string, HashSet<int>, Application> generator, bool allowManualRun, ImageData icon = null, bool save = true)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            else if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            Name = name;
            Generator = generator;
            AllowManualRun = allowManualRun;
            Icon = icon;
            if (save)
                Saver = new ApplicationSaver(this);
        }

        public Application this[int index]
        {
            get
            {
                if (Instances.TryGetValue(index, out Application app))
                    return app;
                return null;
            }
        }

        public void Load() => Saver?.UDBRead(TUI.WorldID);

        public override string ToString() => Name;

        public Application CreateInstance(int x, int y, HashSet<int> observers = null)
        {
            lock (Locker)
            {
                int index = 0;
                while (Instances.ContainsKey(index))
                    index++;
                Application instance = Generator($"{Name}_{index}", observers);
                instance.Type = this;
                instance.Index = index;
                Instances[index] = instance;
                instance.SetXY(x - instance.Width / 2, y - instance.Height / 2, false);
                instance.SavePanel();
                TUI.Create(instance);
                if (!instance.Personal)
                    Saver?.UDBWrite(TUI.WorldID);
                return instance;
            }
        }

        internal void LoadInstance(int index)
        {
            lock (Locker)
            {
                Application instance = Generator($"{Name}_{index}", null);
                instance.Type = this;
                instance.Index = index;
                if (Instances.TryGetValue(index, out Application another))
                {
                    TUI.Destroy(another);
                    TUI.Log($"Application {another.Name} was replaced by a new one.", LogType.Warning);
                }
                Instances[index] = instance;
                TUI.Create(instance);
            }
        }

        internal void DisposeInstance(Application app)
        {
            lock (Locker)
            {
                Application instance = Instances[app.Index];
                instance.EndPlayerSession();
                Instances.Remove(app.Index);
                if (!instance.Personal)
                    Saver?.UDBWrite(TUI.WorldID);
            }
        }

        public void DestroyAll()
        {
            foreach (var pair in IterateInstances.ToArray())
                TUI.Destroy(pair.Value);
        }

        public bool TryDestroy(int x, int y, out string name)
        {
            name = null;
            foreach (var pair in IterateInstances)
                if (pair.Value.Contains(x, y))
                {
                    name = pair.Value.Name;
                    TUI.Destroy(pair.Value);
                    return true;
                }
            return false;
        }

        public void Write(BinaryWriter bw)
        {
            lock (Locker)
            {
                var instances = Instances.Where(instance => !instance.Value.Personal);
                bw.Write((byte)instances.Count());
                foreach (var pair in instances)
                    bw.Write((int)pair.Key);
            }
        }
    }
}
