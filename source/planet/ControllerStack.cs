namespace OrbitRing {
    sealed partial class Controller {
        public bool InsideStack { get { return ring!=null && ring.CanGoBack; } }
        public string StackName { get { return ring==null?"planet":ring.StackName; } }
        public void EnterStack(ActionItem item) { if(ring!=null) ring.OpenStack(item); }
        public void BackStack() { if(ring==null || !ring.NavigateBack()) HideRing(); }
        void ClearStack() { ActiveItems=Config.Items; }
    }
}
