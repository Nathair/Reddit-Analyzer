import { useState, useEffect } from 'react';

function SubredditInput({ item, index, updateSubreddit, updateKeywords, removeSubreddit }) {
    const [searchQuery, setSearchQuery] = useState(item.subreddit);
    const [searchResults, setSearchResults] = useState([]);
    const [isSearching, setIsSearching] = useState(false);
    const [showDropdown, setShowDropdown] = useState(false);

    const [lastSelected, setLastSelected] = useState(item.subreddit);

    useEffect(() => {
        // Only search if the query is long enough and hasn't just been selected
        if (!searchQuery || searchQuery.length < 3 || searchQuery === lastSelected) {
            setSearchResults([]);
            return;
        }

        const timer = setTimeout(async () => {
            setIsSearching(true);
            try {
                // Encode the query in case of spaces or special characters
                const response = await fetch(`/api/reddit/search?query=${encodeURIComponent(searchQuery)}`);
                if (response.ok) {
                    const data = await response.json();
                    setSearchResults(data);
                    // Only show dropdown if user hasn't selected something in the meantime
                    setShowDropdown(data.length > 0 && searchQuery !== lastSelected);
                }
            } catch (err) {
                console.error("Search error", err);
            } finally {
                setIsSearching(false);
            }
        }, 1000);

        return () => clearTimeout(timer);
    }, [searchQuery, lastSelected]);

    const handleSelect = (name) => {
        setLastSelected(name);
        setSearchQuery(name);
        updateSubreddit(index, name);
        setSearchResults([]);
        setShowDropdown(false);
    };

    const handleChange = (val) => {
        setSearchQuery(val);
        updateSubreddit(index, val);
    };

    return (
        <div className="input-group">
            <button type="button" className="btn btn-danger remove-btn" onClick={() => removeSubreddit(index)}>✕</button>
            <div className="input-row">
                <div className="autocomplete-wrapper">
                    <input 
                        placeholder="Subreddit (e.g. r/nature)" 
                        value={item.subreddit}
                        onChange={(e) => handleChange(e.target.value)}
                        onFocus={() => item.subreddit && searchResults.length > 0 && setShowDropdown(true)}
                        onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
                        required
                    />
                    {isSearching && (
                        <div style={{ position: 'absolute', right: '10px', top: '10px' }}>
                            <span className="loader" style={{ width: '16px', height: '16px' }}></span>
                        </div>
                    )}
                    {showDropdown && (
                        <div className="autocomplete-dropdown">
                            {searchResults.map((res, i) => (
                                <div key={i} className="autocomplete-item" onClick={() => handleSelect(res)}>
                                    {res}
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </div>
            <div className="input-row">
                <input 
                    placeholder="Keywords (comma separated)" 
                    value={item.keywords.join(', ')}
                    onChange={(e) => updateKeywords(index, e.target.value)}
                />
            </div>
        </div>
    );
}

function App() {
    const [items, setItems] = useState([
        { subreddit: 'r/nature', keywords: ['forest', 'river'] },
        { subreddit: 'r/aww', keywords: ['cat', 'dog'] }
    ]);
    const [limit, setLimit] = useState(25);
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState(null);
    const [error, setError] = useState(null);

    const addSubreddit = () => {
        setItems([...items, { subreddit: '', keywords: [] }]);
    };

    const removeSubreddit = (index) => {
        setItems(items.filter((_, i) => i !== index));
    };

    const updateSubreddit = (index, value) => {
        const newItems = [...items];
        newItems[index].subreddit = value;
        setItems(newItems);
    };

    const updateKeywords = (index, value) => {
        const newItems = [...items];
        newItems[index].keywords = value.split(',').map(k => k.trim()).filter(k => k !== '');
        setItems(newItems);
    };

    const handleAnalyze = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError(null);
        setResults(null);

        try {
            const response = await fetch(`/api/reddit?verbose=true`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ items, limit })
            });

            if (!response.ok) throw new Error('Failed to analyze');
            
            const data = await response.json();
            setResults(data);
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    const handleDownload = async () => {
        try {
            const response = await fetch(`/api/reddit/download?verbose=true`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ items, limit })
            });

            if (!response.ok) throw new Error('Download failed');

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = "reddit_analysis.json";
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
        } catch (err) {
            setError(err.message);
        }
    };

    return (
        <div className="reddit-analyzer">
            <h1>Reddit Analyzer 🚀</h1>
            <p className="text-muted">Analyze subreddits and filter posts by keywords.</p>

            <form onSubmit={handleAnalyze} className="subreddit-form">
                {items.map((item, index) => (
                    <SubredditInput 
                        key={index}
                        index={index}
                        item={item}
                        updateSubreddit={updateSubreddit}
                        updateKeywords={updateKeywords}
                        removeSubreddit={removeSubreddit}
                    />
                ))}

                <div className="controls">
                    <button type="button" className="btn btn-outline" onClick={addSubreddit}>+ Add Subreddit</button>
                    <div style={{ flex: 1 }}></div>
                    <label style={{ marginRight: '1rem', color: '#818384' }}>Limit N:</label>
                    <input 
                        type="number" 
                        value={limit} 
                        onChange={(e) => setLimit(parseInt(e.target.value))}
                        style={{ width: '80px', flex: 'none' }}
                        min="1" max="100"
                    />
                </div>

                <div className="controls" style={{ marginTop: '1.5rem', borderTop: '1px solid #343536', paddingTop: '1.5rem' }}>
                    <div style={{ flex: 1 }}></div>
                    <button type="button" className="btn btn-outline" onClick={handleDownload} disabled={loading}>Download JSON</button>
                    <button type="submit" className="btn btn-primary" disabled={loading}>
                        {loading ? 'Analyzing...' : 'Run Analysis'}
                        {loading && <span className="loader"></span>}
                    </button>
                </div>
            </form>

            {error && <div style={{ color: '#ff4444', marginBottom: '1rem' }}>Error: {error}</div>}

            {results && results.subreddits && (
                <div className="results-area">
                    <h2 style={{ borderBottom: '2px solid #ff4500', paddingBottom: '0.5rem' }}>Results</h2>
                    {Object.entries(results.subreddits).map(([sub, subData]) => (
                        <div key={sub} className="subreddit-results">
                            <h3 className="subreddit-title">{sub} <span className="text-muted" style={{fontSize: '0.9rem'}}>({subData.count} matches)</span></h3>
                            <ul className="posts-list">
                                {subData.posts.map((post, i) => (
                                    <li key={i} className="post-item">
                                        <div className="post-title">{post.title}</div>
                                        <div className="post-meta">
                                            {post.hasImage && post.imageUrl && (
                                                <span 
                                                    className="badge" 
                                                    style={{ '--preview-url': `url(${post.imageUrl})` }}
                                                >
                                                    Image
                                                </span>
                                            )}
                                            {post.postUrl && (
                                                <a href={post.postUrl} target="_blank" rel="noreferrer" style={{color: '#24a0ed', textDecoration: 'none'}}>View Post</a>
                                            )}
                                        </div>
                                    </li>
                                ))}
                                {subData.posts.length === 0 && <li className="text-muted">No matches found with given keywords.</li>}
                            </ul>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

export default App;