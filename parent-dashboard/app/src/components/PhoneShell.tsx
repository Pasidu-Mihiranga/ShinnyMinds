import type { ReactNode } from 'react';

export default function PhoneShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-svh w-full bg-[#eceafc] flex justify-center">
      <div className="relative w-full max-w-[480px] min-h-svh h-svh bg-white shadow-xl overflow-hidden flex flex-col">
        {children}
      </div>
    </div>
  );
}
