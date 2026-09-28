import { Link } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <section aria-labelledby="not-found-heading">
      <h1 id="not-found-heading">Page Not Found</h1>
      <p>The page you're looking for doesn't exist or may have been moved.</p>
      <p>
        <Link to="/">Go to dashboard</Link>
      </p>
    </section>
  );
}
