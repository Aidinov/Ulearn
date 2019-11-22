import React, { Component } from "react";
import PropTypes from "prop-types";

import Button from "@skbkontur/react-ui/Button";
import LeftIcon from '@skbkontur/react-icons/ArrowChevron2Left';

import { groupAsStudentType, progressType } from "../../types";
import LinksToGroupsStatements from "../../LinksToGroupsStatements/LinksToGroupsStatements";

import styles from './NavigationHeader.less';
import ProgressBar from "../../ProgressBar";

class NavigationHeader extends Component {
	render() {
		const { createRef, groupsAsStudent, } = this.props;
		return (
			<header ref={ (ref) => createRef(ref) } className={ styles.root }>
				{ this.renderBreadcrumb() }
				{ this.renderTitle() }
				{ this.renderProgress() }
				{ groupsAsStudent.length > 0 && <LinksToGroupsStatements groupsAsStudent={ groupsAsStudent }/> }
			</header>
		);
	}

	renderBreadcrumb() {
		const { courseName, onCourseClick } = this.props;

		return (
			<nav className={ styles.breadcrumbs }>
				<Button
					use="link"
					icon={ <LeftIcon/> }
					onClick={ onCourseClick }>{ courseName }</Button>
			</nav>
		);
	}

	renderTitle() {
		const { title } = this.props;

		return <h2 className={ styles.h2 } title={ title }>{ title }</h2>;
	}

	renderProgress() {
		const { progress } = this.props;
		const percentage = progress.current / progress.max;

		if (percentage > 0) {
			return (
				<div className={ styles.progressBarWrapper } title={ `${ progress.current } из ${ progress.max }` }>
					<ProgressBar value={ percentage } color={ percentage >= 1 ? 'green' : 'blue' }/>
				</div>
			);
		}
	}
}

NavigationHeader.propTypes = {
	title: PropTypes.string.isRequired,
	courseName: PropTypes.string,
	progress: PropTypes.shape(progressType),
	groupsAsStudent: PropTypes.arrayOf(PropTypes.shape(groupAsStudentType)),
	onCourseClick: PropTypes.func,
};

export default NavigationHeader
