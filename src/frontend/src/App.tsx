import { QueryClientProvider } from '@tanstack/react-query';
import { AppRouter } from './app/AppRouter';
import { ErrorBoundary } from './app/ErrorBoundary';
import { queryClient } from './app/queryClient';
import { ThemeProvider } from './app/theme/ThemeProvider';
import { MotionProvider } from './app/motion/MotionProvider';
import { SignalRProvider } from './app/realtime/SignalRProvider';
import { Toaster } from './components/ui/sonner';

function App() {
  return (
    <ErrorBoundary>
      <ThemeProvider>
        <MotionProvider>
          <QueryClientProvider client={queryClient}>
            <SignalRProvider>
              <AppRouter />
            </SignalRProvider>
            <Toaster />
          </QueryClientProvider>
        </MotionProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
