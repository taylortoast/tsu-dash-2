(function () {
  "use strict";

  function validatePost(post, options) {
    const errors = [];
    const settings = options || {};
    required(post.title, "Title", 150, errors);
    required(post.pointOfContact, "Point of contact", 150, errors);
    required(post.description, "Description", 4000, errors);
    if (settings.requireLatestUpdate !== false) {
      required(post.latestUpdate, "Latest update", 4000, errors);
    }
    if (!post.estimatedCompletionDate) {
      errors.push("Estimated completion date is required.");
    }
    return errors;
  }

  function required(value, label, maxLength, errors) {
    const text = (value || "").trim();
    if (!text) {
      errors.push(`${label} is required.`);
    } else if (text.length > maxLength) {
      errors.push(`${label} must be ${maxLength} characters or fewer.`);
    }
  }

  window.TSU = window.TSU || {};
  window.TSU.validation = { validatePost };
}());
