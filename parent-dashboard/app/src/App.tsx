import { HashRouter, Routes, Route } from 'react-router-dom';
import PhoneShell from './components/PhoneShell';
import BottomNav from './components/BottomNav';
import Home from './screens/Home';
import Insights from './screens/Insights';
import Assistant from './screens/Assistant';
import Settings from './screens/Settings';
import SignIn from './screens/SignIn';
import { AuthProvider, useAuth } from './auth/AuthContext';

export default function App() {
  return (
    <AuthProvider>
      <HashRouter>
        <PhoneShell>
          <Shell />
        </PhoneShell>
      </HashRouter>
    </AuthProvider>
  );
}

/**
 * Chooses what fills the phone frame. The tab bar only exists once a parent is signed
 * in, so there is no route that reaches a child's data unauthenticated.
 */
function Shell() {
  const { status } = useAuth();

  if (status === 'loading') {
    return (
      <div className="flex-1 grid place-items-center">
        <div className="w-9 h-9 rounded-full border-[3px] border-violet-200 border-t-violet-600 animate-spin" />
      </div>
    );
  }

  if (status === 'signed-out') {
    return <SignIn />;
  }

  return (
    <>
      <div className="flex-1 overflow-y-auto overscroll-contain min-h-0">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/insights" element={<Insights />} />
          <Route path="/assistant" element={<Assistant />} />
          <Route path="/settings" element={<Settings />} />
        </Routes>
      </div>
      <BottomNav />
    </>
  );
}
