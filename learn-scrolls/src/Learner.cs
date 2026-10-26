using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.UI;
using Kingmaker.UI.Common;
#if WOTR
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
#endif

namespace LearnScrolls
{
    // Asks in the game's own dialog, with the list of who learns what, then copies the scrolls the way the game's button does.
    static class Learner
    {
        // After the dialog closes, whatever the answer, done gets the number of the spells and recipes learned.
        internal static void Ask(List<Lesson> lessons, Action<int> done)
        {
            if (lessons.Count == 0) return;
            // The game's window asks the question; the list of who learns what goes into it under the text.
            LessonList.Expect(Strings.Question, lessons);
#if KINGMAKER
            UIUtility.ShowMessageBox(Strings.Question, DialogMessageBoxBase.BoxType.Dialog, button =>
            {
                Finish(lessons, done, button == DialogMessageBoxBase.BoxButton.Yes);
            });
#else
            UIUtility.ShowMessageBox(Strings.Question, MessageModalBase.ModalType.Dialog, button =>
            {
                Finish(lessons, done, button == MessageModalBase.ButtonType.Yes);
            });
#endif
        }

        // A message in the game's own window, with OK only.
        internal static void Tell(string text)
        {
#if KINGMAKER
            UIUtility.ShowMessageBox(text, DialogMessageBoxBase.BoxType.Message, button => { });
#else
            UIUtility.ShowMessageBox(text, MessageModalBase.ModalType.Message, button => { });
#endif
        }

        static void Finish(List<Lesson> lessons, Action<int> done, bool yes)
        {
            LessonList.Close();
            int learned = yes ? Learn(lessons) : 0;
            try { if (done != null) done(learned); }
            catch (Exception e) { Main.Error("after learning", e); }
        }

        static int Learn(List<Lesson> lessons)
        {
            int learned = 0;
            try
            {
                foreach (var lesson in lessons)
                {
                    if (lesson.Recipe != null)
                    {
                        var item = Game.Instance.Player.Inventory.FirstOrDefault(i => i.Blueprint.GetComponent<CopyRecipe>() == lesson.Recipe);
                        if (item != null && lesson.Recipe.CanCopy(item, null))
                        {
                            lesson.Recipe.Copy(item, null);
                            learned++;
                        }
                        continue;
                    }
                    // Another item each time: the stack the last one came from may be spent.
                    var scroll = Planner.ItemFor(lesson.Spell, lesson.Unit);
                    if (scroll == null) continue;
                    scroll.Blueprint.GetComponent<CopyScroll>().Copy(scroll, lesson.Unit);
                    learned++;
                }
            }
            catch (Exception e)
            {
                Main.Error("learn", e);
            }
            if (learned > 0)
            {
#if KINGMAKER
                Game.Instance.UI.Common.UISound.Play(UISoundType.SubscribeItem);
#else
                UISoundController.Instance.Play(UISoundType.SubscribeItem);
#endif
            }
            return learned;
        }
    }
}
