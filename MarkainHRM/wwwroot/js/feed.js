// --- SignalR Connection ---
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/voteHub")
    .build();

connection.on("ReceiveVoteUpdate", function (postId, newScore) {
    // Update the score in the DOM
    const scoreSpan = document.querySelector(`#post-${postId} .like-count`);
    if (scoreSpan) {
        scoreSpan.innerText = newScore;
        // Visual feedback (flash effect)
        scoreSpan.classList.add('scale-150', 'text-yellow-500'); // Pop effect
        setTimeout(() => {
            scoreSpan.classList.remove('scale-150', 'text-yellow-500');
        }, 300);
    }
});

connection.on("ReceiveCommentVoteUpdate", function (commentId, newScore) {
    const scoreSpan = document.querySelector(`.comment-like-count[data-comment-id="${commentId}"]`);
    if (scoreSpan) {
        scoreSpan.innerText = newScore;
        // Visual feedback
        scoreSpan.classList.add('scale-150');
        setTimeout(() => {
            scoreSpan.classList.remove('scale-150');
        }, 300);
    }
});


connection.on("ReceiveNewComment", function (postId, commentData) {
    const list = document.getElementById(`comment-list-${postId}`);
    // If the comment list exists (post is on screen), append it
    if (list) {
        const avatarHtml = commentData.avatar
            ? `<img src="${commentData.avatar}" class="w-6 h-6 rounded-full object-cover" alt="Avatar" />`
            : `<div class="w-6 h-6 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-xs font-bold text-slate-500">${commentData.user[0].toUpperCase()}</div>`;

        const html = `
            <div class="flex gap-2 mb-2 animate-fade-in" id="comment-${commentData.id}">
                <div class="flex flex-col items-center pt-1 w-10 flex-shrink-0">
                     <button onclick="voteComment(${commentData.id}, 1, this)" class="vote-btn transition-all p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-400 hover:text-orange-500"><i data-lucide="arrow-up" class="w-5 h-5"></i></button>
                     <span class="comment-like-count text-xs font-bold my-0.5 text-slate-600 dark:text-slate-400" data-comment-id="${commentData.id}">0</span>
                     <button onclick="voteComment(${commentData.id}, -1, this)" class="vote-btn transition-all p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-400 hover:text-blue-500"><i data-lucide="arrow-down" class="w-5 h-5"></i></button>
                </div>
                <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 mb-1">
                         ${avatarHtml}
                         <span class="font-semibold text-sm text-slate-800 dark:text-slate-200">${commentData.user}</span>
                         <span class="text-xs text-slate-500">${commentData.date}</span>
                    </div>
                    <div id="comment-content-${commentData.id}" class="text-sm text-slate-700 dark:text-slate-300 mb-2 whitespace-pre-wrap">${commentData.content}</div>
                    
                    <div class="flex items-center gap-3 text-xs font-semibold text-slate-500 mt-1">
                         <button onclick="toggleReplyForm(${commentData.id})" class="hover:text-[#004B87] dark:hover:text-blue-400 transition-colors"><i data-lucide="corner-down-right" class="w-3 h-3 inline mr-1"></i>Reply</button>
                         <button onclick="toggleEditComment(${commentData.id})" class="hover:text-slate-800 dark:hover:text-slate-200 transition-colors">Edit</button>
                         <button onclick="deleteComment(${commentData.id})" class="hover:text-red-600 dark:hover:text-red-400 transition-colors">Delete</button>
                    </div>

                    <div id="reply-form-${commentData.id}" class="hidden mt-2 flex items-center gap-2">
                        <input id="reply-input-${commentData.id}" type="text" class="flex-1 bg-white dark:bg-slate-800 border border-gray-300 dark:border-slate-600 rounded-full px-3 py-1.5 text-xs outline-none focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87]" placeholder="Write a reply..." onkeydown="if(event.key === 'Enter') submitComment(${postId}, ${commentData.id})" />
                        <button onclick="submitComment(${postId}, ${commentData.id})" class="text-[#004B87] dark:text-blue-400 hover:text-blue-700 p-1"><i data-lucide="send" class="w-3 h-3"></i></button>
                    </div>

                    <div id="edit-form-${commentData.id}" class="hidden mt-2">
                         <textarea id="edit-input-${commentData.id}" rows="2" class="w-full bg-white dark:bg-slate-800 border border-gray-300 dark:border-slate-600 rounded-lg px-3 py-2 text-sm outline-none focus:border-[#004B87]" onkeydown="if(event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); submitEditComment(${commentData.id}); }">${commentData.content}</textarea>
                         <div class="flex justify-end gap-2 mt-1">
                             <button onclick="toggleEditComment(${commentData.id})" class="text-xs text-slate-500 font-bold">Cancel</button>
                             <button onclick="submitEditComment(${commentData.id})" class="text-xs bg-[#004B87] text-white px-3 py-1 rounded font-bold">Save</button>
                         </div>
                    </div>

                    <div id="replies-${commentData.id}" class="hidden mt-2 pl-4 border-l-2 border-slate-200 dark:border-slate-700 space-y-3"></div>
                </div>
            </div>
            `;

        // Check if parent comment exists (is reply)
        if (commentData.parentId) {
            const repliesList = document.getElementById(`replies-${commentData.parentId}`);
            if (repliesList) {
                repliesList.classList.remove('hidden');
                repliesList.insertAdjacentHTML('beforeend', html);

                // Update parent collapse button
                const btn = document.getElementById(`collapse-btn-${commentData.parentId}`);
                if (btn) {
                    btn.classList.remove('hidden'); // Ensure visible
                    const text = document.getElementById(`collapse-text-${commentData.parentId}`);
                    const icon = btn.querySelector('i');

                    // Force expand UI state
                    if (text) text.textContent = 'Collapse';
                    if (icon) icon.setAttribute('data-lucide', 'minus-square');
                }
            }
        } else {
            // Top level comment
            list.classList.remove('hidden');
            list.insertAdjacentHTML('beforeend', html);

            // Update count
            const countSpan = document.querySelector(`button[onclick="toggleComments(${postId})"] .comment-count`);
            if (countSpan) {
                let current = parseInt(countSpan.innerText);
                countSpan.parentElement.innerHTML = `<span class="comment-count">${current + 1}</span> Comment${(current + 1) !== 1 ? 's' : ''}`;
            }
        }

        if (typeof lucide !== 'undefined') lucide.createIcons();
    }
});

connection.on("ReceiveCommentDeleted", function (commentId, postId) {
    const el = document.getElementById(`comment-${commentId}`);
    if (el) {
        el.classList.add('opacity-0', 'transition-opacity', 'duration-300');
        setTimeout(() => {
            el.remove();

            // Re-calc top level count? A bit complex if it was a reply.
            // Simplified: If top level, decrement count.
            if (el.parentElement.id.startsWith("comment-list-")) {
                const countSpan = document.querySelector(`button[onclick="toggleComments(${postId})"] .comment-count`);
                if (countSpan) {
                    let current = parseInt(countSpan.innerText);
                    if (current > 0)
                        countSpan.parentElement.innerHTML = `<span class="comment-count">${current - 1}</span> Comment${(current - 1) !== 1 ? 's' : ''}`;
                }
            }
        }, 300);
    }
});

connection.on("ReceiveCommentEdited", function (commentId, content) {
    const contentDiv = document.getElementById(`comment-content-${commentId}`);
    if (contentDiv) {
        contentDiv.innerText = content;
        // visual flash
        contentDiv.classList.add('bg-yellow-100', 'dark:bg-yellow-900/30', 'transition-colors', 'duration-500');
        setTimeout(() => contentDiv.classList.remove('bg-yellow-100', 'dark:bg-yellow-900/30'), 1000);
    }
});

connection.on("ReceivePostEdited", function (postId, content) {
    const contentP = document.querySelector(`#post-${postId} .post-content`);
    if (contentP) {
        contentP.innerText = content;
        // visual flash
        contentP.classList.add('bg-yellow-100', 'dark:bg-yellow-900/30', 'transition-colors', 'duration-500');
        setTimeout(() => contentP.classList.remove('bg-yellow-100', 'dark:bg-yellow-900/30'), 1000);
    }
});

connection.on("ReceivePostDeleted", function (postId) {
    const postEl = document.getElementById(`post-${postId}`);
    if (postEl) {
        postEl.classList.add('opacity-0', 'scale-95', 'transition-all', 'duration-500');
        setTimeout(() => postEl.remove(), 500);
    }
});

connection.start().catch(function (err) {
    return console.error(err.toString());
});


// --- UPLOAD PREVIEWS ---
function previewImage(input) {
    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) {
            document.getElementById('imagePreview').src = e.target.result;
            document.getElementById('imagePreviewContainer').classList.remove('hidden');
        }
        reader.readAsDataURL(input.files[0]);

        // Hide file preview if exists (mutually exclusive UI for simplicity, though backend supports both)
        document.getElementById('filePreviewContainer').classList.add('hidden');
    }
}

function clearImagePreview() {
    document.querySelector('input[name="media"]').value = '';
    document.getElementById('imagePreviewContainer').classList.add('hidden');
}

function previewFile(input) {
    if (input.files && input.files[0]) {
        document.getElementById('fileNamePreview').textContent = input.files[0].name;
        document.getElementById('filePreviewContainer').classList.remove('hidden');
        document.getElementById('filePreviewContainer').classList.add('flex');

        // Hide image preview
        document.getElementById('imagePreviewContainer').classList.add('hidden');
    }
}

function clearFilePreview() {
    document.querySelector('input[name="attachment"]').value = '';
    document.getElementById('filePreviewContainer').classList.add('hidden');
    document.getElementById('filePreviewContainer').classList.remove('flex');
}


// --- SOCIAL INTERACTIONS ---
function vote(postId, value, btn) {
    const container = btn.parentElement;
    const upBtn = container.children[0];
    const scoreSpan = container.children[1];
    const downBtn = container.children[2];
    const upIcon = upBtn.querySelector('svg') || upBtn.querySelector('i');
    const downIcon = downBtn.querySelector('svg') || downBtn.querySelector('i');

    let currentScore = parseInt(scoreSpan.innerText);

    // Determine current state based on classes
    let isUpvoted = upIcon.classList.contains('text-blue-600') && upIcon.classList.contains('fill-blue-600');
    let isDownvoted = downIcon.classList.contains('text-red-700') && downIcon.classList.contains('fill-red-700'); // Bloodred check

    // Optimistic Update Logic
    if (value === 1) { // Upvote clicked
        if (isUpvoted) {
            // Remove Upvote
            upIcon.classList.remove('text-blue-600', 'fill-blue-600');
            upIcon.classList.add('text-slate-400', 'dark:text-slate-500');
            scoreSpan.classList.remove('text-blue-600');
            scoreSpan.classList.add('text-slate-700', 'dark:text-slate-300');
            currentScore--;
        } else {
            // Add Upvote (Remove downvote if exists)
            if (isDownvoted) {
                downIcon.classList.remove('text-red-700', 'fill-red-700');
                downIcon.classList.add('text-slate-400', 'dark:text-slate-500');
                currentScore++; // Neutralize downvote
            }
            upIcon.classList.remove('text-slate-400', 'dark:text-slate-500');
            upIcon.classList.add('text-blue-600', 'fill-blue-600');

            scoreSpan.classList.remove('text-slate-700', 'dark:text-slate-300', 'text-red-700');
            scoreSpan.classList.add('text-blue-600');

            // Animation
            upIcon.classList.add('scale-125');
            setTimeout(() => upIcon.classList.remove('scale-125'), 200);

            currentScore++;
        }
    } else if (value === -1) { // Downvote clicked
        if (isDownvoted) {
            // Remove Downvote
            downIcon.classList.remove('text-red-700', 'fill-red-700');
            downIcon.classList.add('text-slate-400', 'dark:text-slate-500');
            scoreSpan.classList.remove('text-red-700');
            scoreSpan.classList.add('text-slate-700', 'dark:text-slate-300');
            currentScore++;
        } else {
            // Add Downvote (Remove upvote if exists)
            if (isUpvoted) {
                upIcon.classList.remove('text-blue-600', 'fill-blue-600');
                upIcon.classList.add('text-slate-400', 'dark:text-slate-500');
                currentScore--; // Neutralize upvote
            }
            downIcon.classList.remove('text-slate-400', 'dark:text-slate-500');
            downIcon.classList.add('text-red-700', 'fill-red-700');

            scoreSpan.classList.remove('text-slate-700', 'dark:text-slate-300', 'text-blue-600');
            scoreSpan.classList.add('text-red-700');

            // Animation
            downIcon.classList.add('scale-125');
            setTimeout(() => downIcon.classList.remove('scale-125'), 200);

            currentScore--;
        }
    }

    scoreSpan.innerText = currentScore;

    fetch('/Collaboration/Vote', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: `postId=${postId}&value=${value}`
    })
        .then(res => res.json())
        .then(data => {
            // Server Sync
            scoreSpan.innerText = data.score;

            // Updates Colors based on server 'userVote'
            // Reset all
            upIcon.classList.remove('text-blue-600', 'fill-blue-600');
            upIcon.classList.add('text-slate-400', 'dark:text-slate-500');
            downIcon.classList.remove('text-red-700', 'fill-red-700');
            downIcon.classList.add('text-slate-400', 'dark:text-slate-500');

            scoreSpan.className = "font-bold text-sm min-w-[20px] text-center like-count";

            if (data.userVote === 1) {
                upIcon.classList.remove('text-slate-400', 'dark:text-slate-500');
                upIcon.classList.add('text-blue-600', 'fill-blue-600');
                scoreSpan.classList.add('text-blue-600');
            } else if (data.userVote === -1) {
                downIcon.classList.remove('text-slate-400', 'dark:text-slate-500');
                downIcon.classList.add('text-red-700', 'fill-red-700');
                scoreSpan.classList.add('text-red-700');
            } else {
                scoreSpan.classList.add('text-slate-700', 'dark:text-slate-300');
            }
        });
}

function voteComment(commentId, value, btn) {
    const container = btn.parentElement;
    const upBtn = container.children[0];
    const scoreSpan = container.children[1];
    const downBtn = container.children[2];

    let currentScore = parseInt(scoreSpan.innerText);

    // Determine current state (Reddit colors: orange=up, blue=down)
    let isUpvoted = upBtn.classList.contains('text-orange-500');
    let isDownvoted = downBtn.classList.contains('text-blue-500');

    // Optimistic Update
    if (value === 1) {
        if (isUpvoted) {
            // Toggle off upvote
            upBtn.classList.remove('text-orange-500');
            upBtn.classList.add('text-slate-400', 'hover:text-orange-500');
            scoreSpan.classList.remove('text-orange-500');
            scoreSpan.classList.add('text-slate-600', 'dark:text-slate-400');
            currentScore--;
        } else {
            if (isDownvoted) {
                // Switch from downvote to upvote
                downBtn.classList.remove('text-blue-500');
                downBtn.classList.add('text-slate-400', 'hover:text-blue-500');
                currentScore += 2; // Remove downvote AND add upvote
            } else {
                currentScore++;
            }
            upBtn.classList.remove('text-slate-400', 'hover:text-orange-500');
            upBtn.classList.add('text-orange-500');
            scoreSpan.classList.remove('text-slate-600', 'dark:text-slate-400', 'text-blue-500');
            scoreSpan.classList.add('text-orange-500');
        }
    } else {
        if (isDownvoted) {
            // Toggle off downvote
            downBtn.classList.remove('text-blue-500');
            downBtn.classList.add('text-slate-400', 'hover:text-blue-500');
            scoreSpan.classList.remove('text-blue-500');
            scoreSpan.classList.add('text-slate-600', 'dark:text-slate-400');
            currentScore++;
        } else {
            if (isUpvoted) {
                // Switch from upvote to downvote
                upBtn.classList.remove('text-orange-500');
                upBtn.classList.add('text-slate-400', 'hover:text-orange-500');
                currentScore -= 2; // Remove upvote AND add downvote
            } else {
                currentScore--;
            }
            downBtn.classList.remove('text-slate-400', 'hover:text-blue-500');
            downBtn.classList.add('text-blue-500');
            scoreSpan.classList.remove('text-slate-600', 'dark:text-slate-400', 'text-orange-500');
            scoreSpan.classList.add('text-blue-500');
        }
    }

    scoreSpan.innerText = currentScore;

    fetch('/Collaboration/VoteComment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: `commentId=${commentId}&value=${value}`
    })
        .then(res => res.json())
        .then(data => {
            scoreSpan.innerText = data.score;

            // Reset all states
            upBtn.classList.remove('text-orange-500', 'text-slate-400', 'hover:text-orange-500');
            downBtn.classList.remove('text-blue-500', 'text-slate-400', 'hover:text-blue-500');
            scoreSpan.className = "comment-like-count text-xs font-bold my-0.5";

            if (data.userVote === 1) {
                upBtn.classList.add('text-orange-500');
                scoreSpan.classList.add('text-orange-500');
                downBtn.classList.add('text-slate-400', 'hover:text-blue-500');
            } else if (data.userVote === -1) {
                downBtn.classList.add('text-blue-500');
                scoreSpan.classList.add('text-blue-500');
                upBtn.classList.add('text-slate-400', 'hover:text-orange-500');
            } else {
                upBtn.classList.add('text-slate-400', 'hover:text-orange-500');
                downBtn.classList.add('text-slate-400', 'hover:text-blue-500');
                scoreSpan.classList.add('text-slate-600', 'dark:text-slate-400');
            }
        });
}

function toggleComments(postId) {
    console.log("Toggling comments for post:", postId);
    const section = document.getElementById(`comments-${postId}`);
    if (section) {
        section.classList.toggle('hidden');
        if (!section.classList.contains('hidden')) {
            if (typeof lucide !== 'undefined') lucide.createIcons();
            setTimeout(() => {
                const input = document.getElementById(`comment-input-${postId}`);
                if (input) input.focus();
            }, 100);
        }
    } else {
        console.error("Comments section not found for post:", postId);
    }
}

function toggleReplyForm(commentId) {
    const form = document.getElementById(`reply-form-${commentId}`);
    if (form) {
        form.classList.toggle('hidden');
        if (!form.classList.contains('hidden')) {
            setTimeout(() => {
                const input = document.getElementById(`reply-input-${commentId}`);
                if (input) input.focus();
            }, 100);
        }
    }
}

function toggleCommentThread(commentId) {
    const repliesContainer = document.getElementById(`replies-${commentId}`);
    const collapseBtn = document.getElementById(`collapse-btn-${commentId}`);
    const collapseText = document.getElementById(`collapse-text-${commentId}`);

    if (!repliesContainer || !collapseBtn) return;

    const icon = collapseBtn.querySelector('i');

    if (repliesContainer.classList.contains('hidden')) {
        // Expand
        repliesContainer.classList.remove('hidden');
        if (collapseText) collapseText.textContent = 'Collapse';
        if (icon) icon.setAttribute('data-lucide', 'minus-square');
    } else {
        // Collapse
        repliesContainer.classList.add('hidden');
        const replyCount = repliesContainer.querySelectorAll('[id^="comment-"]').length;
        if (collapseText) collapseText.textContent = `Expand (${replyCount} ${replyCount === 1 ? 'reply' : 'replies'})`;
        if (icon) icon.setAttribute('data-lucide', 'plus-square');
    }

    if (typeof lucide !== 'undefined') lucide.createIcons();
}

// Show more replies (for collapsed comment chains)
function showMoreReplies(commentId) {
    const viewMoreBtn = document.getElementById(`view-more-${commentId}`);
    const hiddenReplies = document.getElementById(`hidden-replies-${commentId}`);

    if (viewMoreBtn && hiddenReplies) {
        // Hide the "view more" button
        viewMoreBtn.classList.add('hidden');

        // Show hidden replies with animation
        hiddenReplies.classList.remove('hidden');
        hiddenReplies.classList.add('animate-in', 'fade-in', 'duration-300');

        // Initialize lucide icons for newly shown content
        if (typeof lucide !== 'undefined') lucide.createIcons();
    }
}

function toggleEditComment(commentId) {
    const form = document.getElementById(`edit-form-${commentId}`);
    if (form) form.classList.toggle('hidden');
}

function submitEditComment(commentId) {
    const input = document.getElementById(`edit-input-${commentId}`);
    if (!input) return;
    const content = input.value.trim();
    if (!content) return;

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Collaboration/EditComment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: `commentId=${commentId}&content=${encodeURIComponent(content)}`
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                const contentDiv = document.getElementById(`comment-content-${commentId}`);
                if (contentDiv) contentDiv.innerText = data.content;
                toggleEditComment(commentId);
            }
        })
        .catch(err => console.error("Edit failed", err));
}

// --- DELETE COMMENT LOGIC (MODAL) ---
function openDeleteCommentModal(commentId) {
    document.getElementById('deleteCommentId').value = commentId;
    document.getElementById('deleteCommentModal').classList.remove('hidden');
}

function closeDeleteCommentModal() {
    document.getElementById('deleteCommentModal').classList.add('hidden');
}

function deleteComment(commentId) {
    // New logic used button helper, but if direct call:
    openDeleteCommentModal(commentId);
}

function confirmDeleteComment() {
    const commentId = document.getElementById('deleteCommentId').value;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Collaboration/DeleteComment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: `commentId=${commentId}`
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                const el = document.getElementById(`comment-${commentId}`);
                if (el) {
                    el.classList.add('opacity-0', 'transition-opacity', 'duration-300');
                    setTimeout(() => el.remove(), 300);
                }
                closeDeleteCommentModal();
            }
        })
        .catch(err => console.error("Delete failed", err));
}

function submitComment(postId, parentId = null) {
    const inputId = parentId ? `reply-input-${parentId}` : `comment-input-${postId}`;
    const input = document.getElementById(inputId);
    if (!input) return;

    const content = input.value.trim();
    if (!content) return;

    input.value = '';

    let body = `postId=${postId}&content=${encodeURIComponent(content)}`;
    if (parentId) body += `&parentCommentId=${parentId}`;

    fetch('/Collaboration/AddComment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: body
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                // SignalR will handle the UI update (ReceiveNewComment)
                // Just ensuring input is cleared (already done above) and maybe closing reply form if needed
                if (parentId) {
                    toggleReplyForm(parentId);
                }
            } else {
                console.error("Comment submission reported failure", data);
            }
        })
        .catch(err => console.error("Comment failed", err));
}

function sharePost(postId) {
    const btn = document.getElementById(`share-btn-${postId}`);
    if (!btn) return;

    // Check if share box already exists
    let shareBox = document.getElementById(`share-box-${postId}`);
    if (shareBox) {
        shareBox.classList.toggle('hidden');
        return;
    }

    // Create Share Menu (now with two options)
    shareBox = document.createElement('div');
    shareBox.id = `share-box-${postId}`;
    shareBox.className = 'absolute bottom-12 right-0 w-44 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-2xl z-50 p-1.5 animate-in fade-in slide-in-from-bottom-2 duration-150';

    // Opt 1: Share on Feed
    const feedBtn = document.createElement('button');
    feedBtn.className = 'w-full text-left px-3 py-2.5 rounded-lg hover:bg-emerald-50 dark:hover:bg-emerald-900/20 text-slate-700 dark:text-slate-200 text-[10px] font-bold uppercase tracking-wider transition-all flex items-center gap-2';
    feedBtn.innerHTML = '<i data-lucide="rss" class="w-3.5 h-3.5 text-emerald-500"></i> Confirm Share';
    feedBtn.onclick = () => {
        performShareOnFeed(postId);
        shareBox.classList.add('hidden');
    };

    // Opt 2: Send to Chat
    const chatBtn = document.createElement('button');
    chatBtn.className = 'w-full text-left px-3 py-2.5 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/20 text-slate-700 dark:text-slate-200 text-[10px] font-bold uppercase tracking-wider transition-all flex items-center gap-2 border-t border-slate-100 dark:border-slate-700 mt-1 pt-2';
    chatBtn.innerHTML = '<i data-lucide="message-square" class="w-3.5 h-3.5 text-blue-500"></i> Send to Chat';
    chatBtn.onclick = () => {
        openChatSelector(postId);
        shareBox.classList.add('hidden');
    };

    shareBox.appendChild(feedBtn);
    shareBox.appendChild(chatBtn);

    // Position relative to the button
    const container = btn.parentElement;
    container.style.position = 'relative';
    container.appendChild(shareBox);

    if (typeof lucide !== 'undefined') lucide.createIcons();

    // Close on click outside
    document.addEventListener('click', function closeBox(e) {
        if (!shareBox.contains(e.target) && e.target !== btn && !btn.contains(e.target)) {
            shareBox.classList.add('hidden');
            document.removeEventListener('click', closeBox);
        }
    });
}

function openChatSelector(postId) {
    // Create Modal if not exists
    let modal = document.getElementById('chat-selector-modal');
    if (!modal) {
        modal = document.createElement('div');
        modal.id = 'chat-selector-modal';
        modal.className = 'fixed inset-0 bg-black/60 backdrop-blur-sm z-[100] flex items-center justify-center p-4 hidden';
        modal.innerHTML = `
            <div class="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 w-full max-w-sm rounded-2xl shadow-2xl p-6 animate-in zoom-in-95 duration-200">
                <div class="flex items-center justify-between mb-4">
                    <h3 class="font-bold text-slate-800 dark:text-white flex items-center gap-2">
                        <i data-lucide="send" class="w-4 h-4 text-[#004B87] dark:text-blue-400"></i>
                        Share to Chat
                    </h3>
                    <button onclick="document.getElementById('chat-selector-modal').classList.add('hidden')" class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
                        <i data-lucide="x" class="w-5 h-5"></i>
                    </button>
                </div>
                <div id="chat-list-container" class="max-h-80 overflow-y-auto custom-scrollbar space-y-2">
                    <div class="py-10 text-center text-slate-400 italic text-sm">Loading chats...</div>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
        if (typeof lucide !== 'undefined') lucide.createIcons();
    }

    modal.classList.remove('hidden');
    const list = document.getElementById('chat-list-container');
    list.innerHTML = '<div class="py-10 text-center text-slate-400 italic text-sm">Loading chats...</div>';

    fetch('/Collaboration/GetUserChats')
        .then(res => res.json())
        .then(chats => {
            list.innerHTML = '';
            if (chats.length === 0) {
                list.innerHTML = '<div class="py-10 text-center text-slate-400 text-sm">No active chats found.</div>';
                return;
            }

            chats.forEach(chat => {
                const item = document.createElement('button');
                item.className = 'w-full flex items-center gap-3 p-3 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-800 transition-all border border-transparent hover:border-slate-200 dark:hover:border-slate-700 text-left group';

                const avatar = chat.photoUrl
                    ? `<img src="${chat.photoUrl}" class="w-10 h-10 rounded-lg object-cover shadow-sm" />`
                    : `<div class="w-10 h-10 rounded-lg bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-500 dark:text-slate-400 font-bold text-xs border border-slate-200 dark:border-slate-700">${chat.name[0].toUpperCase()}</div>`;

                item.innerHTML = `
                    ${avatar}
                    <div class="flex-1 min-w-0">
                        <p class="text-sm font-semibold text-slate-800 dark:text-slate-100 truncate">${chat.name}</p>
                        <p class="text-[10px] text-slate-400 uppercase font-bold tracking-wider">${chat.isPrivate ? 'Private Chat' : 'Group Chat'}</p>
                    </div>
                    <i data-lucide="chevron-right" class="w-4 h-4 text-slate-300 group-hover:translate-x-1 transition-transform"></i>
                `;
                item.onclick = () => performShareToChat(postId, chat.id, chat.isPrivate);
                list.appendChild(item);
            });
            if (typeof lucide !== 'undefined') lucide.createIcons();
        })
        .catch(err => {
            console.error("Failed to load chats", err);
            list.innerHTML = '<div class="py-10 text-center text-red-500 text-sm">Failed to load chats.</div>';
        });
}

function performShareToChat(postId, chatId, isPrivate) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Collaboration/ShareToChat', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: `postId=${postId}&chatId=${chatId}&isPrivate=${isPrivate}`
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showToast('Post shared to chat!');
                document.getElementById('chat-selector-modal').classList.add('hidden');
            } else {
                showToast(data.message || 'Error sharing post', 'error');
            }
        })
        .catch(err => {
            console.error("Share to chat failed", err);
            showToast('Error sharing post', 'error');
        });
}

function performShareOnFeed(postId) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Collaboration/ShareOnFeed', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: `postId=${postId}`
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                showToast('Shared to your feed!');
                const box = document.getElementById(`share-box-${postId}`);
                if (box) box.classList.add('hidden');
            } else {
                showToast(data.message || 'Error sharing post', 'error');
            }
        })
        .catch(err => {
            console.error("Share failed", err);
            showToast('Error sharing post', 'error');
        });
}

function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    const bgColor = type === 'success' ? 'bg-slate-800' : 'bg-red-600';
    toast.className = `fixed bottom-4 right-4 ${bgColor} text-white px-4 py-2 rounded-lg shadow-lg text-sm z-[100] animate-in fade-in slide-in-from-bottom-4 duration-300`;
    toast.innerText = message;
    document.body.appendChild(toast);
    setTimeout(() => {
        toast.classList.add('opacity-0', 'transition-opacity', 'duration-500');
        setTimeout(() => toast.remove(), 500);
    }, 3000);
}

// --- POST MANAGEMENT ---
function togglePostMenu(postId) {
    document.querySelectorAll('[id^="post-menu-"]').forEach(el => {
        if (el.id !== `post-menu-${postId}`) el.classList.add('hidden');
    });
    const menu = document.getElementById(`post-menu-${postId}`);
    if (menu) menu.classList.toggle('hidden');
}

document.addEventListener('click', function (e) {
    if (!e.target.closest('button[onclick^="togglePostMenu"]')) {
        document.querySelectorAll('[id^="post-menu-"]').forEach(el => el.classList.add('hidden'));
    }
});

function openEditPostModal(postId, content) {
    document.getElementById('editPostId').value = postId;
    document.getElementById('editPostContent').value = content;
    document.getElementById('editPostModal').classList.remove('hidden');
}

function closeEditPostModal() {
    document.getElementById('editPostModal').classList.add('hidden');
}

function openDeletePostModal(postId) {
    document.getElementById('deletePostId').value = postId;
    document.getElementById('deletePostModal').classList.remove('hidden');
}

function closeDeletePostModal() {
    document.getElementById('deletePostModal').classList.add('hidden');
}

// Deep Linking Handler
function handleDeepLink() {
    const hash = window.location.hash;
    if (hash) {
        const targetId = hash.substring(1);
        let targetElement = document.getElementById(targetId);

        if (targetElement) {
            if (targetId.startsWith("comment-")) {
                const postCommentsSection = targetElement.closest('[id^="comments-"]');
                if (postCommentsSection) {
                    postCommentsSection.classList.remove('hidden');
                }

                let parent = targetElement.parentElement;
                while (parent && !parent.id.startsWith("comment-list-")) {
                    if (parent.id.startsWith("replies-")) {
                        parent.classList.remove('hidden');
                        // Update collapse button
                        const commentId = parent.id.split('-')[1];
                        const btn = document.getElementById(`collapse-btn-${commentId}`);
                        if (btn) {
                            const text = document.getElementById(`collapse-text-${commentId}`);
                            const icon = btn.querySelector('i');
                            if (text) text.textContent = 'Collapse';
                            if (icon) icon.setAttribute('data-lucide', 'minus-square');
                        }
                    }
                    parent = parent.parentElement;
                }
                if (typeof lucide !== 'undefined') lucide.createIcons();
            }

            setTimeout(() => {
                targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                targetElement.classList.add('ring-2', 'ring-offset-2', 'ring-[#004B87]', 'dark:ring-blue-400', 'transition-all', 'duration-500');
                setTimeout(() => {
                    targetElement.classList.remove('ring-2', 'ring-offset-2', 'ring-[#004B87]', 'dark:ring-blue-400');
                }, 2500);
            }, 100);
        }
    }
}

document.addEventListener("DOMContentLoaded", handleDeepLink);
window.addEventListener("hashchange", handleDeepLink);

// --- Ozark Student Hub Search Suggestions ---
let _searchTimer;
window.searchUsersGlobal = function (query, element) {
    console.log("OZARK_SEARCH: Input detected:", query);
    const input = element || document.getElementById('globalUserSearch');
    if (!input) return;

    // Ensure portal exists
    let portal = document.getElementById('globalSearchResultsPortal');
    if (!portal) {
        portal = document.createElement('div');
        portal.id = 'globalSearchResultsPortal';
        portal.className = "fixed bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-2xl overflow-hidden hidden z-[99999]";
        document.body.appendChild(portal);
    }

    const icon = input.parentElement.querySelector('i');

    if (!query || query.trim().length < 1) {
        portal.classList.add('hidden');
        if (icon) icon.classList.remove('animate-spin', 'text-blue-500');
        return;
    }

    // Position
    const rect = input.getBoundingClientRect();
    portal.style.top = (rect.bottom + window.scrollY + 8) + 'px';
    portal.style.left = (rect.left + window.scrollX) + 'px';
    portal.style.width = rect.width + 'px';

    // Show loading
    if (icon) icon.classList.add('animate-spin', 'text-blue-500');
    portal.innerHTML = '<div class="p-6 text-center text-slate-400 text-sm">Searching...</div>';
    portal.classList.remove('hidden');

    clearTimeout(_searchTimer);
    _searchTimer = setTimeout(() => {
        console.log("OZARK_SEARCH: Fetching results for:", query);
        fetch(`/Collaboration/SearchGlobal?query=${encodeURIComponent(query.trim())}`)
            .then(r => r.json())
            .then(data => {
                console.log("OZARK_SEARCH: Data received:", data);
                if (icon) icon.classList.remove('animate-spin', 'text-blue-500');

                if (!Array.isArray(data) || data.length === 0) {
                    portal.innerHTML = '<div class="p-8 text-center text-slate-400 text-sm">No results found</div>';
                    return;
                }

                portal.innerHTML = data.map(item => `
                    <div class="flex items-center p-3 hover:bg-slate-50 dark:hover:bg-slate-700/50 cursor-pointer border-b border-slate-100 dark:border-slate-700 last:border-0 group" 
                             onclick="window.location.href='${item.type === 'user' ? '/Account/Profile?userId=' + item.id : '/Collaboration/Details/' + item.id}'">
                        <div class="w-10 h-10 rounded-xl overflow-hidden bg-slate-100 dark:bg-slate-900 flex-shrink-0 flex items-center justify-center text-lg font-bold text-slate-400">
                            ${item.photo ? `<img src="${item.photo}" class="w-full h-full object-cover">` : (item.name || '?')[0]}
                        </div>
                        <div class="ml-3 flex-1 min-w-0">
                            <p class="text-sm font-semibold text-slate-800 dark:text-slate-200 truncate">${item.name}</p>
                            <p class="text-[10px] text-slate-400 uppercase font-bold tracking-widest">${item.type}</p>
                        </div>
                        <i data-lucide="chevron-right" class="w-4 h-4 text-slate-300 group-hover:translate-x-1 transition-transform"></i>
                    </div>
                `).join('');

                if (typeof lucide !== 'undefined') lucide.createIcons();
            })
            .catch(err => {
                console.error("OZARK_SEARCH: Fetch failed:", err);
                if (icon) icon.classList.remove('animate-spin', 'text-blue-500');
                portal.innerHTML = '<div class="p-8 text-center text-red-400 text-sm">Search temporary unavailable</div>';
            });
    }, 300);
}

// Global listeners
document.addEventListener('mousedown', e => {
    const p = document.getElementById('globalSearchResultsPortal');
    if (p && !p.contains(e.target) && !e.target.closest('#globalUserSearch')) {
        p.classList.add('hidden');
    }
});
